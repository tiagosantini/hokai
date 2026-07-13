namespace Hokai.Services;

/// <summary>Executes native OS processes with safe argument handling.</summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs a process with the given executable and arguments.
    /// Captures stdout and stderr. The caller's cancellation token terminates the process tree.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
