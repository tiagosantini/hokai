using Hokai.Hosting;

namespace Hokai.Services;

/// <summary>Environment needed by every service manager backend.</summary>
public sealed class ServiceManagerContext
{
    public ApplicationPaths Paths { get; init; } = new();
    public IProcessRunner ProcessRunner { get; init; } = new ProcessRunner();
    public string ExecutablePath { get; init; } = "";
    public string SudoUserName { get; init; } = "";
    public string HomeDirectory { get; init; } = "";
    public bool IsElevated { get; init; }
}
