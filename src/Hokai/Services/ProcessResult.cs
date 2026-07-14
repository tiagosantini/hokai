namespace Hokai.Services;

/// <summary>Encapsulates the result of a native process execution.</summary>
public sealed class ProcessResult
{
    /// <summary>The exit code reported by the process.</summary>
    public int ExitCode { get; init; }

    /// <summary>Captured standard output text.</summary>
    public string StandardOutput { get; init; } = "";

    /// <summary>Captured standard error text.</summary>
    public string StandardError { get; init; } = "";
}
