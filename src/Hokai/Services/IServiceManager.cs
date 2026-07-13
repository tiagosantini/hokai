namespace Hokai.Services;

/// <summary>Manages OS-level service lifecycles across supported platforms.</summary>
public interface IServiceManager
{
    /// <summary>Registers the application as an OS service without starting it.</summary>
    Task InstallAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the OS service registration. When <paramref name="purge"/> is <c>true</c>,
    /// configuration and data directories are also removed.
    /// </summary>
    Task UninstallAsync(bool purge, CancellationToken cancellationToken = default);

    /// <summary>Starts the installed OS service.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the running OS service.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a human-readable summary of the current OS service state.</summary>
    Task<string> GetStatusAsync(CancellationToken cancellationToken = default);
}
