using Hokai.Models;
using Microsoft.Extensions.Configuration;

namespace Hokai.Hosting;

public static class AppSettingsLoader
{
    public static AppSettings Load(string configPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables("HOKAI_")
            .Build();

        var settings = new AppSettings();
        configuration.Bind(settings);

        Validate(settings, configPath);

        var normalizedDataDirectory = NormalizeDataDirectory(settings.DataDirectory, configPath);
        if (normalizedDataDirectory != settings.DataDirectory)
        {
            settings = new AppSettings
            {
                Smtp = settings.Smtp,
                DataDirectory = normalizedDataDirectory,
                RetentionDays = settings.RetentionDays
            };
        }

        return settings;
    }

    public static AppSettings LoadDefaults()
    {
        return new AppSettings
        {
            DataDirectory = "Data",
            Smtp = new SmtpSettings()
        };
    }

    /// <summary>
    /// Validates settings after loading. Throws on configuration that would
    /// silently misbehave at runtime.
    /// </summary>
    public static void Validate(AppSettings settings, string configPath)
    {
        if (settings.RetentionDays <= 0)
            throw new InvalidOperationException(
                $"RetentionDays must be positive (got {settings.RetentionDays}). Config: {configPath}");

        if (!string.IsNullOrEmpty(settings.Smtp.Host) && settings.Smtp.Port <= 0)
            throw new InvalidOperationException(
                $"Smtp.Port must be positive when Smtp.Host is configured. Config: {configPath}");
    }

    /// <summary>
    /// If DataDirectory is relative, make it relative to the config file's directory,
    /// not the process working directory.
    /// </summary>
    private static string NormalizeDataDirectory(string dataDirectory, string configPath)
    {
        if (Path.IsPathRooted(dataDirectory))
            return dataDirectory;

        var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath));
        if (configDir is null)
            return dataDirectory;

        return Path.GetFullPath(Path.Combine(configDir, dataDirectory));
    }
}
