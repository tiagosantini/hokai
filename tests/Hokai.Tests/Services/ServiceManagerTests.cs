using Hokai.Hosting;
using Hokai.Services;

namespace Hokai.Tests.Services;

public sealed class ServiceManagerTests
{
    private const int ServiceNotActiveCode = 1062;
    private const int ServiceAlreadyRunningCode = 1056;

    [Fact]
    public void Facade_SelectsBackendForCurrentPlatform()
    {
        var context = CreateContext();

        var service = new ServiceManager(context);

        Assert.NotNull(service);
    }

    [Fact]
    public async Task Launchd_Install_NotElevated_Succeeds()
    {
        var runner = new FakeProcessRunner();
        var tmpRoot = Path.Combine(Path.GetTempPath(), $"hokai-launchd-{Guid.NewGuid():N}");
        var homeDir = Path.Combine(tmpRoot, "Users", "testuser");
        Directory.CreateDirectory(homeDir);
        try
        {
            var ctx = new ServiceManagerContext
            {
                Paths = new ApplicationPaths
                {
                    ConfigPath = Path.Combine(homeDir, "Library", "Application Support", "Hokai", "appsettings.json"),
                    ConfigDirectory = Path.Combine(homeDir, "Library", "Application Support", "Hokai"),
                    DataDirectory = Path.Combine(homeDir, "Library", "Application Support", "Hokai", "Data"),
                    DefinitionPath = Path.Combine(homeDir, "Library", "LaunchAgents", "com.hokai.daemon.plist")
                },
                ProcessRunner = runner,
                ExecutablePath = "/usr/local/bin/hokai",
                SudoUserName = "testuser",
                HomeDirectory = homeDir,
                IsElevated = false
            };
            var service = new LaunchdServiceManager(ctx);

            await service.InstallAsync();
        }
        finally
        {
            SafeDelete(tmpRoot);
        }
    }

    [Fact]
    public async Task Systemd_GetStatus_NotInstalled_ReturnsNotInstalled()
    {
        var runner = new FakeProcessRunner();
        var ctx = CreateContext(runner: runner);
        var service = new SystemdServiceManager(ctx);

        var status = await service.GetStatusAsync();

        Assert.Equal("not installed", status);
    }

    [Fact]
    public async Task Systemd_GetStatus_Active_ReturnsActiveLabel()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("systemctl", ["is-active", "hokai"],
            new ProcessResult { ExitCode = 0, StandardOutput = "active\n" });
        var ctx = CreateContext(runner: runner, definitionExists: true);
        var service = new SystemdServiceManager(ctx);

        var status = await service.GetStatusAsync();

        Assert.Equal("active (active)", status);
    }

    [Fact]
    public async Task Systemd_GetStatus_Inactive_ReturnsInactiveLabel()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("systemctl", ["is-active", "hokai"],
            new ProcessResult { ExitCode = 3, StandardOutput = "inactive\n" });
        var ctx = CreateContext(runner: runner, definitionExists: true);
        var service = new SystemdServiceManager(ctx);

        var status = await service.GetStatusAsync();

        Assert.Equal("inactive (inactive)", status);
    }

    [Fact]
    public async Task Systemd_Start_Success_DoesNotThrow()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("systemctl", ["start", "hokai"],
            new ProcessResult { ExitCode = 0 });
        var ctx = CreateContext(runner: runner);
        var service = new SystemdServiceManager(ctx);

        await service.StartAsync();
    }

    [Fact]
    public async Task Systemd_Start_NonZeroExitCode_Throws()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("systemctl", ["start", "hokai"],
            new ProcessResult { ExitCode = 1, StandardError = "permission denied" });
        var ctx = CreateContext(runner: runner);
        var service = new SystemdServiceManager(ctx);

        var ex = await Assert.ThrowsAsync<ServiceManagerException>(() => service.StartAsync());
        Assert.Contains("permission denied", ex.Message);
    }

    [Fact]
    public async Task Systemd_Stop_Success_DoesNotThrow()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("systemctl", ["stop", "hokai"],
            new ProcessResult { ExitCode = 0 });
        var ctx = CreateContext(runner: runner);
        var service = new SystemdServiceManager(ctx);

        await service.StopAsync();
    }

    [Fact]
    public async Task Systemd_Install_NotElevated_ThrowsServiceManagerException()
    {
        var ctx = CreateContext(isElevated: false);
        var service = new SystemdServiceManager(ctx);

        await Assert.ThrowsAsync<ServiceManagerException>(() => service.InstallAsync());
    }

    [Fact]
    public async Task Uninstall_PurgeFalse_DoesNotRemoveDirectories()
    {
        var runner = new FakeProcessRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"hokai-test-{Guid.NewGuid():N}");
        var configDir = Path.Combine(tempDir, "config");
        var dataDir = Path.Combine(tempDir, "data");
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(dataDir);
        try
        {
            var ctx = CreateContext(runner: runner, isElevated: true, configDir: configDir, dataDir: dataDir);
            var service = new ServiceManager(ctx);

            await service.UninstallAsync(purge: false);

            Assert.True(Directory.Exists(configDir));
            Assert.True(Directory.Exists(dataDir));
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    [Fact]
    public async Task Uninstall_PurgeTrue_RemovesDirectories()
    {
        var runner = new FakeProcessRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"hokai-test-{Guid.NewGuid():N}");
        var configDir = Path.Combine(tempDir, "config");
        var dataDir = Path.Combine(tempDir, "data");
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(dataDir);
        try
        {
            var ctx = CreateContext(runner: runner, isElevated: true, configDir: configDir, dataDir: dataDir);
            var service = new ServiceManager(ctx);

            await service.UninstallAsync(purge: true);

            Assert.False(Directory.Exists(configDir));
            Assert.False(Directory.Exists(dataDir));
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    [Fact]
    public async Task InstallAsync_GeneratesDefaultConfig_WithCorrectDataDirectory()
    {
        var runner = new FakeProcessRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"hokai-test-{Guid.NewGuid():N}");
        var configDir = Path.Combine(tempDir, "config");
        var configPath = Path.Combine(configDir, "appsettings.json");
        try
        {
            var paths = new ApplicationPaths
            {
                ConfigPath = configPath,
                ConfigDirectory = configDir,
                DataDirectory = Path.Combine(tempDir, "data"),
                DefinitionPath = Path.Combine(tempDir, "hokai.service")
            };
            var ctx = new ServiceManagerContext
            {
                Paths = paths,
                ProcessRunner = runner,
                ExecutablePath = "/usr/local/bin/hokai",
                IsElevated = true,
                SudoUserName = ""
            };
            var service = new SystemdServiceManager(ctx);
            await service.InstallAsync();

            var configContent = File.ReadAllText(configPath);
            Assert.Contains("\"DataDirectory\": \"/var/lib/hokai\"", configContent);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    [Fact]
    public async Task InstallAsync_Linux_ChownsConfigFileAfterCreation()
    {
        if (!OperatingSystem.IsLinux()) return;

        var runner = new FakeProcessRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"hokai-test-{Guid.NewGuid():N}");
        var configDir = Path.Combine(tempDir, "config");
        var configPath = Path.Combine(configDir, "appsettings.json");
        try
        {
            var paths = new ApplicationPaths
            {
                ConfigPath = configPath,
                ConfigDirectory = configDir,
                DataDirectory = Path.Combine(tempDir, "data"),
                DefinitionPath = Path.Combine(tempDir, "hokai.service")
            };
            var ctx = new ServiceManagerContext
            {
                Paths = paths,
                ProcessRunner = runner,
                ExecutablePath = "/usr/local/bin/hokai",
                IsElevated = true,
                SudoUserName = ""
            };
            var service = new SystemdServiceManager(ctx);
            await service.InstallAsync();

            Assert.True(File.Exists(configPath));
            Assert.Contains(runner.Invocations,
                i => i.Executable == "chown"
                     && i.Args.Length >= 2
                     && i.Args[0] == "hokai:hokai"
                     && i.Args[1] == configPath);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    [Fact]
    public async Task InstallAsync_Linux_ConfigFileIsWorldReadable()
    {
        if (!OperatingSystem.IsLinux()) return;

        var runner = new FakeProcessRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"hokai-test-{Guid.NewGuid():N}");
        var configDir = Path.Combine(tempDir, "config");
        var configPath = Path.Combine(configDir, "appsettings.json");
        try
        {
            var paths = new ApplicationPaths
            {
                ConfigPath = configPath,
                ConfigDirectory = configDir,
                DataDirectory = Path.Combine(tempDir, "data"),
                DefinitionPath = Path.Combine(tempDir, "hokai.service")
            };
            var ctx = new ServiceManagerContext
            {
                Paths = paths,
                ProcessRunner = runner,
                ExecutablePath = "/usr/local/bin/hokai",
                IsElevated = true,
                SudoUserName = ""
            };
            var service = new SystemdServiceManager(ctx);
            await service.InstallAsync();

            Assert.True(File.Exists(configPath));
            var mode = File.GetUnixFileMode(configPath);
            Assert.True(mode.HasFlag(UnixFileMode.OtherRead),
                "Config file should be world-readable (UnixFileMode.OtherRead)");
            Assert.True(mode.HasFlag(UnixFileMode.GroupRead),
                "Config file should be group-readable (UnixFileMode.GroupRead)");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    [Fact]
    public void PlatformContext_Detect_ReturnsValidData()
    {
        var platform = PlatformContext.Detect();

        Assert.NotNull(platform.ExecutablePath);
        Assert.NotEmpty(platform.ExecutablePath);
        Assert.NotNull(platform.UserName);
        Assert.NotEmpty(platform.UserName);
    }

    [Fact]
    public async Task Stop_AlreadyStopped_DoesNotThrow()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("sc.exe", ["stop", "Hokai"],
            new ProcessResult { ExitCode = ServiceNotActiveCode });
        var ctx = CreateContext(runner: runner);
        var service = new WindowsServiceManager(ctx);

        await service.StopAsync();
    }

    [Fact]
    public async Task WindowsDefaultConfig_UsesAbsoluteDataDirectory()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("sc.exe", ["query", "Hokai"],
            new ProcessResult { ExitCode = 1 });
        var tempDir = Path.Combine(Path.GetTempPath(), $"hokai-test-{Guid.NewGuid():N}");
        var configDir = Path.Combine(tempDir, "config");
        var configPath = Path.Combine(configDir, "appsettings.json");
        try
        {
            var paths = new ApplicationPaths
            {
                ConfigPath = configPath,
                ConfigDirectory = configDir,
                DataDirectory = Path.Combine(tempDir, "data"),
                DefinitionPath = Path.Combine(tempDir, "hokai.service")
            };
            var ctx = new ServiceManagerContext
            {
                Paths = paths,
                ProcessRunner = runner,
                ExecutablePath = @"C:\Program Files\Hokai\hokai.exe",
                IsElevated = true,
                SudoUserName = ""
            };
            var service = new WindowsServiceManager(ctx);
            await service.InstallAsync();

            var configContent = File.ReadAllText(configPath);
            Assert.Contains("\"DataDirectory\":", configContent);

            var dataDirStart = configContent.IndexOf("\"DataDirectory\":") + "\"DataDirectory\":".Length;
            var dataDirValue = configContent[dataDirStart..];
            dataDirValue = dataDirValue.Trim().TrimStart('"').Split('"')[0];

            Assert.True(Path.IsPathRooted(dataDirValue),
                $"DataDirectory '{dataDirValue}' should be an absolute path");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    private static ServiceManagerContext CreateContext(
        IProcessRunner? runner = null,
        bool isElevated = true,
        bool definitionExists = false,
        string? configDir = null,
        string? dataDir = null)
    {
        var cfgDir = configDir ?? Path.Combine("/tmp", $"hokai-cfg-{Guid.NewGuid():N}");
        var datDir = dataDir ?? Path.Combine("/tmp", $"hokai-data-{Guid.NewGuid():N}");
        var defPath = Path.Combine(cfgDir, "hokai.service");

        if (definitionExists)
        {
            Directory.CreateDirectory(cfgDir);
            File.WriteAllText(defPath, "[Service]");
        }

        return new ServiceManagerContext
        {
            Paths = new ApplicationPaths
            {
                ConfigPath = Path.Combine(cfgDir, "appsettings.json"),
                ConfigDirectory = cfgDir,
                DataDirectory = datDir,
                DefinitionPath = defPath
            },
            ProcessRunner = runner ?? new FakeProcessRunner(),
            ExecutablePath = "/usr/local/bin/hokai",
            SudoUserName = Environment.UserName,
            IsElevated = isElevated
        };
    }

    private static void SafeDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        private readonly Dictionary<string, ProcessResult> _results = new();
        public List<(string Executable, string[] Args)> Invocations { get; } = new();

        public void AddResult(string executable, string[] args, ProcessResult result)
        {
            _results[Key(executable, args)] = result;
        }

        public Task<ProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var args = arguments.ToArray();
            Invocations.Add((executable, args));

            var key = Key(executable, arguments);
            if (_results.TryGetValue(key, out var result))
                return Task.FromResult(result);

            return Task.FromResult(new ProcessResult { ExitCode = 0 });
        }

        private static string Key(string executable, IReadOnlyList<string> args)
            => executable + "\0" + string.Join("\0", args);
    }
}
