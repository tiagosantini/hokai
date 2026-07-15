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

        process.Start();

        // Read stdout and stderr in parallel before awaiting exit to prevent
        // pipe-buffer deadlocks when the child writes more than the OS buffer.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        // Wait for both streams to drain after the process has exited or been killed.
        await Task.WhenAll(stdoutTask, stderrTask);

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdoutTask.Result,
            StandardError = stderrTask.Result
        };
    }
}
