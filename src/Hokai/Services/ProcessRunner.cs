using System.Diagnostics;

namespace Hokai.Services;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(executable))
            throw new ArgumentException("Executable path must not be null or empty.", nameof(executable));
        if (arguments is null)
            throw new ArgumentNullException(nameof(arguments));

        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };

        var output = new StringWriter();
        var error = new StringWriter();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                output.WriteLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                error.WriteLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            // Cancel the async reads so buffered events fire synchronously on the thread,
            // then allow a generous window for the last event handlers to complete. This
            // avoids the race where the process exits but OutputDataReceived has not yet
            // flushed on macOS ARM64 or other slow configurations.
            process.CancelOutputRead();
            process.CancelErrorRead();
            // Wait for the process to fully exit and for any remaining buffered
            // async reads to flush.
            process.WaitForExit();
            await Task.Delay(200, CancellationToken.None);
        }

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = output.ToString(),
            StandardError = error.ToString()
        };
    }
}
