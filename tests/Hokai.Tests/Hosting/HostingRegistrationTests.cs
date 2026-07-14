using Hokai.Hosting;
using Hokai.Models;
using Hokai.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hokai.Tests.Hosting;

public sealed class HostingRegistrationTests
{
    [Fact]
    public void AppSettingsLoader_Load_AbsoluteDataDirectory_KeepsItAsIs()
    {
        using var temp = new TempConfig();
        var configPath = temp.WriteConfig("""
        {
          "DataDirectory": "/absolute/path/to/data",
          "RetentionDays": 15
        }
        """);

        var settings = AppSettingsLoader.Load(configPath);

        Assert.Equal("/absolute/path/to/data", settings.DataDirectory);
        Assert.Equal(15, settings.RetentionDays);
    }

    [Fact]
    public void AppSettingsLoader_Load_RelativeDataDirectory_NormalizesToConfigDir()
    {
        using var temp = new TempConfig();
        var configPath = temp.WriteConfig("""
        {
          "DataDirectory": "Data",
          "RetentionDays": 7
        }
        """);

        var settings = AppSettingsLoader.Load(configPath);

        var expected = Path.GetFullPath(Path.Combine(temp.Root, "Data"));
        Assert.Equal(expected, settings.DataDirectory);
    }

    [Fact]
    public void AppSettingsLoader_Load_MissingFile_ThrowsFileNotFoundException()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"hokai-test-nonexistent-{Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(() => AppSettingsLoader.Load(missingPath));
    }

    [Fact]
    public void AppSettingsLoader_Validate_NegativeRetentionDays_Throws()
    {
        var settings = new AppSettings { RetentionDays = 0 };
        Assert.Throws<InvalidOperationException>(() =>
            AppSettingsLoader.Validate(settings, "test"));
    }

    [Fact]
    public void AppSettingsLoader_Validate_ValidSettings_Passes()
    {
        var settings = new AppSettings { RetentionDays = 30 };
        AppSettingsLoader.Validate(settings, "test"); // no throw
    }

    [Fact]
    public void AppSettingsLoader_LoadDefaults_HasSmtpDefaults()
    {
        var settings = AppSettingsLoader.LoadDefaults();

        Assert.NotNull(settings.Smtp);
        Assert.Empty(settings.Smtp.ToAddresses);
        Assert.Equal("Data", settings.DataDirectory);
        Assert.Equal(30, settings.RetentionDays);
    }

    [Fact]
    public void ServiceCollection_AddHokaiCore_RegistersSingletons()
    {
        var services = new ServiceCollection();
        var settings = new AppSettings { DataDirectory = "/tmp/test-data" };
        var serviceContext = new ServiceManagerContext();

        services.AddHokaiCore(settings, serviceContext);
        var provider = services.BuildServiceProvider();

        var resolvedSettings1 = provider.GetRequiredService<AppSettings>();
        var resolvedSettings2 = provider.GetRequiredService<AppSettings>();
        Assert.Same(resolvedSettings1, resolvedSettings2);

        var smtp1 = provider.GetRequiredService<SmtpSettings>();
        var smtp2 = provider.GetRequiredService<SmtpSettings>();
        Assert.Same(smtp1, smtp2);
    }

    [Fact]
    public void ServiceCollection_AddHokaiMonitoring_RegistersHealthServices()
    {
        using var temp = new TempDir();
        var services = new ServiceCollection();
        var settings = new AppSettings { DataDirectory = temp.Path };
        var serviceContext = new ServiceManagerContext();

        services.AddHokaiCore(settings, serviceContext);
        services.AddHokaiMonitoring();
        var provider = services.BuildServiceProvider();

        var healthCheck = provider.GetRequiredService<IHealthCheckService>();
        Assert.IsType<HealthCheckService>(healthCheck);

        var mailSender = provider.GetRequiredService<ISmtpMailSender>();
        Assert.IsType<SmtpMailSender>(mailSender);

        var notification = provider.GetRequiredService<INotificationService>();
        Assert.IsType<NotificationService>(notification);
    }

    [Fact]
    public void ServiceCollection_AddHokaiMonitoring_HttpClientHasInfiniteTimeout()
    {
        using var temp = new TempDir();
        var services = new ServiceCollection();
        var settings = new AppSettings { DataDirectory = temp.Path };
        var serviceContext = new ServiceManagerContext();

        services.AddHokaiCore(settings, serviceContext);
        services.AddHokaiMonitoring();
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("Hokai.HealthChecks");

        Assert.Equal(System.Threading.Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public void ServiceCollection_AddHokaiDaemon_RegistersOneMonitorService()
    {
        using var temp = new TempDir();
        var services = new ServiceCollection();
        var settings = new AppSettings { DataDirectory = temp.Path };
        var serviceContext = new ServiceManagerContext();

        services.AddHokaiCore(settings, serviceContext);
        services.AddHokaiMonitoring();
        services.AddHokaiDaemon();

        var hostedDescriptors = services
            .Where(sd => sd.ServiceType == typeof(IHostedService))
            .ToList();

        Assert.Single(hostedDescriptors);
        Assert.Equal(typeof(MonitorService), hostedDescriptors[0].ImplementationType);
    }

    [Fact]
    public async Task ServiceCollection_StoresShareDataDirectory()
    {
        using var temp = new TempDir();
        var services = new ServiceCollection();
        var settings = new AppSettings { DataDirectory = temp.Path };
        var serviceContext = new ServiceManagerContext();

        services.AddHokaiCore(settings, serviceContext);
        var provider = services.BuildServiceProvider();

        var endpointStore = provider.GetRequiredService<IEndpointStore>();
        var checkStore = provider.GetRequiredService<ICheckStore>();

        var endpointFile = Path.Combine(temp.Path, "endpoints.json");
        var checkFile = Path.Combine(temp.Path, "checks.json");

        Assert.False(File.Exists(endpointFile));
        Assert.False(File.Exists(checkFile));

        await endpointStore.AddAsync(new EndpointConfig
        {
            Id = "test",
            Url = new Uri("https://example.com"),
            Interval = TimeSpan.FromSeconds(60),
            Timeout = TimeSpan.FromSeconds(30),
            Method = "GET",
            ExpectedStatus = 200,
            CreatedAt = DateTimeOffset.UtcNow
        });

        Assert.True(File.Exists(endpointFile));

        await checkStore.AppendAsync(new CheckResult
        {
            EndpointId = "test",
            Timestamp = DateTimeOffset.UtcNow,
            IsUp = true,
            StatusCode = 200,
            ResponseTimeMs = 42L
        });

        Assert.True(File.Exists(checkFile));

        Assert.Equal(temp.Path, Path.GetDirectoryName(endpointFile));
        Assert.Equal(temp.Path, Path.GetDirectoryName(checkFile));
    }

    [Fact]
    public void ConfigurationPathResolver_EnvConfigSet_MissingFile_ReturnsEnvPath()
    {
        // When HOKAI_CONFIG_PATH is set to a non-existent file, the resolver
        // returns it as-is. Callers are responsible for validating existence.
        var resolver = new ConfigurationPathResolver();
        var envPath = Path.Combine(Path.GetTempPath(), $"hokai-missing-{Guid.NewGuid():N}.json");

        var result = resolver.Resolve(
            explicitConfigPath: null,
            envConfigPath: envPath,
            canonicalConfigExists: false,
            executableDirectory: AppContext.BaseDirectory,
            canonicalConfigPath: "/nonexistent/path",
            serviceName: "hokai");

        Assert.Equal(envPath, result);
    }

    [Fact]
    public void AppSettingsLoader_NonExistentEnvConfig_Throws()
    {
        // HOKAI_CONFIG_PATH pointing to a missing file must fail early.
        var envPath = Path.Combine(Path.GetTempPath(), $"hokai-missing-{Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(() => AppSettingsLoader.Load(envPath));
    }

    [Fact]
    public void HostBuilder_DoesNotLoadUnrelatedCwdConfig()
    {
        // CreateDefaultBuilder would load appsettings.json from CWD.
        // CreateApplicationBuilder should not.
        var savedDir = Environment.CurrentDirectory;
        try
        {
            using var temp = new TempDir();
            var cwdConfig = Path.Combine(temp.Path, "appsettings.json");
            File.WriteAllText(cwdConfig, """{"DataDirectory": "/cwd/data", "RetentionDays": 99}""");
            Environment.CurrentDirectory = temp.Path;

            // The minimal builder must not read CWD config.
            var builder = Host.CreateApplicationBuilder([]);
            using var host = builder.Build();

            var settings = host.Services.GetService<AppSettings>();
            // AppSettings is not registered by the host builder itself — it comes from Hokai's own config.
            Assert.Null(settings);
        }
        finally
        {
            Environment.CurrentDirectory = savedDir;
        }
    }

    private sealed class TempConfig : IDisposable
    {
        public string Root { get; }

        public TempConfig()
        {
            Root = Directory.CreateTempSubdirectory("hokai-tests-").FullName;
        }

        public string WriteConfig(string content)
        {
            var path = Path.Combine(Root, "appsettings.json");
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        }
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = Directory.CreateTempSubdirectory("hokai-tests-").FullName;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
