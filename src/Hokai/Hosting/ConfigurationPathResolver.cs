namespace Hokai.Hosting;

public sealed class ConfigurationPathResolver
{
    /// <summary>
    /// Resolves the configuration file path using the documented priority:
    /// 1. Explicit --config CLI argument
    /// 2. HOKAI_CONFIG_PATH environment variable
    /// 3. Existing canonical OS config (e.g. /etc/hokai/appsettings.json)
    /// 4. appsettings.json next to the executable
    /// 5. Canonical OS config path (even if absent)
    /// </summary>
    public string Resolve(
        string? explicitConfigPath,
        string? envConfigPath,
        bool canonicalConfigExists,
        string executableDirectory,
        string canonicalConfigPath,
        string serviceName)
    {
        if (!string.IsNullOrWhiteSpace(explicitConfigPath))
            return explicitConfigPath;

        if (!string.IsNullOrWhiteSpace(envConfigPath))
            return envConfigPath;

        if (canonicalConfigExists)
            return canonicalConfigPath;

        var adjacent = Path.Combine(executableDirectory, "appsettings.json");

        return adjacent;
    }
}
