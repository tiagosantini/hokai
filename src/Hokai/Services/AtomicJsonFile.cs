using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Hokai.Services;

internal static class AtomicJsonFile
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<List<T>> ReadAsync<T>(
        string path,
        JsonTypeInfo<List<T>> typeInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken)
            ?? throw new JsonException($"'{path}' must contain a JSON array.");
    }

    public static async Task<TResult> MutateAsync<T, TResult>(
        string path,
        JsonTypeInfo<List<T>> typeInfo,
        Func<List<T>, (bool ShouldWrite, TResult Result)> mutation,
        CancellationToken cancellationToken)
    {
        var canonicalPath = Path.GetFullPath(path);
        var pathLock = PathLocks.GetOrAdd(canonicalPath, static _ => new SemaphoreSlim(1, 1));

        await pathLock.WaitAsync(cancellationToken);

        try
        {
            var items = await ReadAsync<T>(canonicalPath, typeInfo, cancellationToken);
            var (shouldWrite, result) = mutation(items);
            if (shouldWrite)
            {
                await WriteAsync(canonicalPath, items, typeInfo, cancellationToken);
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
        JsonTypeInfo<List<T>> typeInfo,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, items, typeInfo, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
