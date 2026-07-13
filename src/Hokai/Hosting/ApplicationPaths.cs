namespace Hokai.Hosting;

public sealed class ApplicationPaths
{
    public string ConfigPath { get; init; } = "";
    public string DataDirectory { get; init; } = "";
    public string DefinitionPath { get; init; } = "";
    public string ConfigDirectory { get; init; } = "";

    public static ApplicationPaths ForLinux(string serviceName)
    {
        return new ApplicationPaths
        {
            ConfigPath = $"/etc/{serviceName}/appsettings.json",
            DataDirectory = $"/var/lib/{serviceName}",
            DefinitionPath = $"/etc/systemd/system/{serviceName}.service",
            ConfigDirectory = $"/etc/{serviceName}"
        };
    }

    public static ApplicationPaths ForMacOS(string userName, string serviceName)
    {
        var baseDir = $"/Users/{userName}/Library/Application Support/Hokai";
        return new ApplicationPaths
        {
            ConfigPath = $"{baseDir}/appsettings.json",
            DataDirectory = $"{baseDir}/Data",
            DefinitionPath = $"/Users/{userName}/Library/LaunchAgents/com.{serviceName}.daemon.plist",
            ConfigDirectory = baseDir
        };
    }

    public static ApplicationPaths ForWindows(string serviceName)
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrEmpty(programData))
            programData = @"C:\ProgramData";

        var baseDir = Path.Combine(programData, serviceName);
        return new ApplicationPaths
        {
            ConfigPath = Path.Combine(baseDir, "appsettings.json"),
            DataDirectory = Path.Combine(baseDir, "Data"),
            ConfigDirectory = baseDir
        };
    }
}
