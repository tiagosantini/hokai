using Hokai.Models;
using Microsoft.Extensions.Configuration;

namespace Hokai.Hosting;

public static class AppSettingsLoader
{
    public static AppSettings Load(string configPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        var settings = new AppSettings();
        configuration.Bind(settings);

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
