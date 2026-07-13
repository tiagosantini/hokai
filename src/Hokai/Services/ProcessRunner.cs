using System.Diagnostics;

namespace Hokai.Services;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
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

        using var registration = cancellationToken.Register(() =>
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        });

        await process.WaitForExitAsync(cancellationToken).WaitAsync(cancellationToken);

        // Unsubscribe from events so no more data arrives after exit.
        process.CancelOutputRead();
        process.CancelErrorRead();

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = output.ToString(),
            StandardError = error.ToString()
        };
    }
}
