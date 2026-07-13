using Hokai.Hosting;

namespace Hokai.Services;

public sealed class ServiceManager : IServiceManager
{
    private readonly IServiceManagerBackend _backend;

    public ServiceManager(ServiceManagerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _backend = SelectBackend(context);
    }

    public ApplicationPaths Paths => _backend.Paths;

    public Task InstallAsync(CancellationToken cancellationToken = default) =>
        _backend.InstallAsync(cancellationToken);

    public Task UninstallAsync(bool purge, CancellationToken cancellationToken = default) =>
        _backend.UninstallAsync(purge, cancellationToken);

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        _backend.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _backend.StopAsync(cancellationToken);

    public Task<string> GetStatusAsync(CancellationToken cancellationToken = default) =>
        _backend.GetStatusAsync(cancellationToken);

    private static IServiceManagerBackend SelectBackend(ServiceManagerContext context)
    {
        if (OperatingSystem.IsLinux())
            return new SystemdServiceManager(context);

        if (OperatingSystem.IsMacOS())
            throw new ServiceManagerException(
                "macOS service management is not yet implemented. Please manage launchd manually.");

        if (OperatingSystem.IsWindows())
            throw new ServiceManagerException(
                "Windows service management is not yet implemented. Please manage the service manually.");

        throw new ServiceManagerException(
            $"Unsupported operating system: {Environment.OSVersion.Platform}.");
    }
}
