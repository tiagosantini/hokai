using Hokai.Hosting;

namespace Hokai.Services;

/// <summary>Platform-specific service manager backend.</summary>
public interface IServiceManagerBackend : IServiceManager
{
    /// <summary>The canonical application paths for this platform.</summary>
    ApplicationPaths Paths { get; }
}
