using System.Collections.Concurrent;
using System.Text.Json;
using Hokai.Serialization;

namespace Hokai.Services;

internal static class AtomicJsonFile
{
    // Store instances share locks by path so a complete read-modify-publish sequence cannot lose
    // another mutation. Cross-process coordination is deliberately outside the storage contract.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Prefer source-generated type metadata when available. The reflection-based fallback
        // remains active so unlisted types still serialize without error. Switching the resolver
        // to the generated context alone would require JsonSerializerIsReflectionEnabledByDefault=false.
        TypeInfoResolver = HokaiJsonContext.Default
    };

    public static async Task<List<T>> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path))
        {
            return [];
        }

        // Readers keep the old snapshot valid while a writer atomically replaces the directory entry.
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken)
            ?? throw new JsonException($"'{path}' must contain a JSON array.");
    }

    public static async Task<TResult> MutateAsync<T, TResult>(
        string path,
        Func<List<T>, (bool ShouldWrite, TResult Result)> mutation,
        CancellationToken cancellationToken)
    {
        var canonicalPath = Path.GetFullPath(path);
        var pathLock = PathLocks.GetOrAdd(canonicalPath, static _ => new SemaphoreSlim(1, 1));

        // The lock covers both reading and publication; locking only the write would permit stale
        // snapshots from concurrent operations to overwrite newer data.
        await pathLock.WaitAsync(cancellationToken);

        try
        {
            var items = await ReadAsync<T>(canonicalPath, cancellationToken);
            var (shouldWrite, result) = mutation(items);
            if (shouldWrite)
            {
                await WriteAsync(canonicalPath, items, cancellationToken);
            }

            return result;
        }
        finally
        {
            pathLock.Release();
        }
    }

    private static async Task WriteAsync<T>(
        string path,
        List<T> items,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        // A unique temporary file in the destination directory keeps publication on one filesystem
        // and prevents concurrent writers from sharing intermediate state.
        var temporaryPath = Path.Combine(
            directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, items, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            // Cancellation is honored before the commit boundary, never after data becomes visible.
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
