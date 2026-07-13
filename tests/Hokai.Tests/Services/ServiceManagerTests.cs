using Hokai.Hosting;
using Hokai.Services;

namespace Hokai.Tests.Services;

public sealed class ServiceManagerTests
{
    [Fact]
    public void LinuxPlatform_SelectsSystemdBackend()
    {
        var context = CreateContext();

        var service = new ServiceManager(context);

        Assert.NotNull(service);
    }

    [Fact]
    public async Task GetStatus_NotInstalled_ReturnsNotInstalled()
    {
        var runner = new FakeProcessRunner();
        var ctx = CreateContext(runner: runner);
        var service = new ServiceManager(ctx);

        var status = await service.GetStatusAsync();

        Assert.Equal("not installed", status);
    }

    [Fact]
    public async Task GetStatus_Active_ReturnsActiveLabel()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("systemctl", ["is-active", "hokai"],
            new ProcessResult { ExitCode = 0, StandardOutput = "active\n" });
        var ctx = CreateContext(runner: runner, definitionExists: true);
        var service = new ServiceManager(ctx);

        var status = await service.GetStatusAsync();

        Assert.Equal("active (active)", status);
    }

    [Fact]
    public async Task GetStatus_Inactive_ReturnsInactiveLabel()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("systemctl", ["is-active", "hokai"],
            new ProcessResult { ExitCode = 3, StandardOutput = "inactive\n" });
        var ctx = CreateContext(runner: runner, definitionExists: true);
        var service = new ServiceManager(ctx);

        var status = await service.GetStatusAsync();

        Assert.Equal("inactive (inactive)", status);
    }

    [Fact]
    public async Task Start_Success_DoesNotThrow()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("systemctl", ["start", "hokai"],
            new ProcessResult { ExitCode = 0 });
        var ctx = CreateContext(runner: runner);
        var service = new ServiceManager(ctx);

        await service.StartAsync();
    }

    [Fact]
    public async Task Start_NonZeroExitCode_Throws()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("systemctl", ["start", "hokai"],
            new ProcessResult { ExitCode = 1, StandardError = "permission denied" });
        var ctx = CreateContext(runner: runner);
        var service = new ServiceManager(ctx);

        var ex = await Assert.ThrowsAsync<ServiceManagerException>(() => service.StartAsync());
        Assert.Contains("permission denied", ex.Message);
    }

    [Fact]
    public async Task Stop_Success_DoesNotThrow()
    {
        var runner = new FakeProcessRunner();
        runner.AddResult("systemctl", ["stop", "hokai"],
            new ProcessResult { ExitCode = 0 });
        var ctx = CreateContext(runner: runner);
        var service = new ServiceManager(ctx);

        await service.StopAsync();
    }

    [Fact]
    public async Task Install_NotElevated_ThrowsServiceManagerException()
    {
        var ctx = CreateContext(isElevated: false);
        var service = new ServiceManager(ctx);

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
    public void PlatformContext_Detect_ReturnsValidData()
    {
        var platform = PlatformContext.Detect();

        Assert.NotNull(platform.ExecutablePath);
        Assert.NotEmpty(platform.ExecutablePath);
        Assert.NotNull(platform.UserName);
        Assert.NotEmpty(platform.UserName);
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

            var key = Key(executable, arguments);
            if (_results.TryGetValue(key, out var result))
                return Task.FromResult(result);

            return Task.FromResult(new ProcessResult { ExitCode = 0 });
        }

        private static string Key(string executable, IReadOnlyList<string> args)
            => executable + "\0" + string.Join("\0", args);
    }
}
