namespace Hokai.Services;

/// <summary>Manages OS-level service lifecycles across supported platforms.</summary>
public interface IServiceManager
{
    /// <summary>Installs the application as an OS service.</summary>
    Task InstallAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes the OS service registration and supporting files.</summary>
    Task UninstallAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts the installed OS service.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the running OS service.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a human-readable summary of the current service state.</summary>
    Task<string> GetStatusAsync(CancellationToken cancellationToken = default);
}
