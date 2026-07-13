using Hokai.Hosting;

namespace Hokai.Tests.Hosting;

public sealed class ApplicationPathsTests
{
    [Fact]
    public void CanonicalConfigPath_Linux_ReturnsEtcHokaiPath()
    {
        var paths = ApplicationPaths.ForLinux("hokai");

        Assert.Equal("/etc/hokai/appsettings.json", paths.ConfigPath);
        Assert.Equal("/var/lib/hokai", paths.DataDirectory);
        Assert.Equal("/etc/systemd/system/hokai.service", paths.DefinitionPath);
    }

    [Fact]
    public void CanonicalConfigPath_MacOS_ReturnsUserLibraryPath()
    {
        var paths = ApplicationPaths.ForMacOS("alice", "hokai");

        Assert.Equal("/Users/alice/Library/Application Support/Hokai/appsettings.json", paths.ConfigPath);
        Assert.Equal("/Users/alice/Library/Application Support/Hokai/Data", paths.DataDirectory);
        Assert.Equal("/Users/alice/Library/LaunchAgents/com.hokai.daemon.plist", paths.DefinitionPath);
    }

    [Fact]
    public void CanonicalConfigPath_Windows_ReturnsProgramDataSubPath()
    {
        var paths = ApplicationPaths.ForWindows("Hokai");

        Assert.EndsWith(Path.Combine("Hokai", "appsettings.json"), paths.ConfigPath);
        Assert.EndsWith(Path.Combine("Hokai", "Data"), paths.DataDirectory);
    }

    [Fact]
    public void ResolveConfigPath_ExplicitCliArg_TakesHighestPriority()
    {
        var resolver = new ConfigurationPathResolver();

        var result = resolver.Resolve(
            explicitConfigPath: "/custom/config.json",
            envConfigPath: null,
            canonicalConfigExists: false,
            executableDirectory: "/usr/local/bin",
            canonicalConfigPath: "/etc/hokai/appsettings.json",
            serviceName: "hokai");

        Assert.Equal("/custom/config.json", result);
    }

    [Fact]
    public void ResolveConfigPath_EnvironmentVariable_TakesSecondPriority()
    {
        var resolver = new ConfigurationPathResolver();

        var result = resolver.Resolve(
            explicitConfigPath: null,
            envConfigPath: "/env/config.json",
            canonicalConfigExists: false,
            executableDirectory: "/usr/local/bin",
            canonicalConfigPath: "/etc/hokai/appsettings.json",
            serviceName: "hokai");

        Assert.Equal("/env/config.json", result);
    }

    [Fact]
    public void ResolveConfigPath_LocalFileExists_TakesThirdPriority()
    {
        var resolver = new ConfigurationPathResolver();

        var result = resolver.Resolve(
            explicitConfigPath: null,
            envConfigPath: null,
            canonicalConfigExists: true,
            executableDirectory: "/usr/local/bin",
            canonicalConfigPath: "/etc/hokai/appsettings.json",
            serviceName: "hokai");

        Assert.Equal("/etc/hokai/appsettings.json", result);
    }

    [Fact]
    public void ResolveConfigPath_NoLocalFile_ReturnsExecutableAdjacent()
    {
        var resolver = new ConfigurationPathResolver();

        var result = resolver.Resolve(
            explicitConfigPath: null,
            envConfigPath: null,
            canonicalConfigExists: false,
            executableDirectory: "/usr/local/bin",
            canonicalConfigPath: "/etc/hokai/appsettings.json",
            serviceName: "hokai");

        Assert.Equal("/usr/local/bin/appsettings.json", result);
    }

    [Fact]
    public void ResolveConfigPath_ExplicitWinsOverEnv()
    {
        var resolver = new ConfigurationPathResolver();

        var result = resolver.Resolve(
            explicitConfigPath: "/cli/config.json",
            envConfigPath: "/env/config.json",
            canonicalConfigExists: true,
            executableDirectory: "/usr/local/bin",
            canonicalConfigPath: "/etc/hokai/appsettings.json",
            serviceName: "hokai");

        Assert.Equal("/cli/config.json", result);
    }
}
