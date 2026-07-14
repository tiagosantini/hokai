using System.CommandLine;
using Hokai.Commands;
using Hokai.Hosting;
using Hokai.Services;

namespace Hokai.Tests.Hosting;

public sealed class HokaiApplicationTests
{
    [Fact]
    public void ParseConfigFlag_ExplicitPath_ReturnsValue()
    {
        var args = new[] { "--config", "/etc/hokai/appsettings.json", "run" };

        var result = HokaiApplication.ParseConfigFlag(args);

        Assert.Equal("/etc/hokai/appsettings.json", result);
    }

    [Fact]
    public void ParseConfigFlag_ShortForm_ReturnsValue()
    {
        var args = new[] { "-c", "/etc/hokai/appsettings.json", "run" };

        var result = HokaiApplication.ParseConfigFlag(args);

        Assert.Equal("/etc/hokai/appsettings.json", result);
    }

    [Fact]
    public void ParseConfigFlag_EqualsForm_ReturnsValue()
    {
        var args = new[] { "--config=/etc/hokai/appsettings.json", "run" };

        var result = HokaiApplication.ParseConfigFlag(args);

        Assert.Equal("/etc/hokai/appsettings.json", result);
    }

    [Fact]
    public void ParseConfigFlag_ShortEqualsForm_ReturnsValue()
    {
        var args = new[] { "-c=/etc/hokai/appsettings.json", "run" };

        var result = HokaiApplication.ParseConfigFlag(args);

        Assert.Equal("/etc/hokai/appsettings.json", result);
    }

    [Fact]
    public void ParseConfigFlag_NoConfig_ReturnsNull()
    {
        var args = new[] { "run" };

        var result = HokaiApplication.ParseConfigFlag(args);

        Assert.Null(result);
    }

    [Fact]
    public void ParseConfigFlag_MissingValue_Throws()
    {
        var args = new[] { "--config" };

        Assert.Throws<InvalidOperationException>(() => HokaiApplication.ParseConfigFlag(args));
    }

    [Fact]
    public void RootCommand_AcceptsConfigOptionBeforeSubcommand()
    {
        using var temp = new TempDir();
        var stores = CreateStores(temp.Path);
        var serviceManager = new FakeServiceManager();

        var configOption = new Option<string?>("--config", ["-c"]);
        var rootCommand = new RootCommand("Hokai — uptime monitoring daemon and CLI");
        rootCommand.Add(configOption);
        rootCommand.Add(EndpointCommands.Create(stores.endpointStore, stores.checkStore));
        rootCommand.Add(StatusCommand.Create(stores.endpointStore, stores.checkStore));
        rootCommand.Add(ServiceCommands.Create(serviceManager));

        var parseResult = rootCommand.Parse("--config /etc/hokai/appsettings.json status");

        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void RootCommand_AcceptsConfigOptionEqualsForm()
    {
        using var temp = new TempDir();
        var stores = CreateStores(temp.Path);
        var serviceManager = new FakeServiceManager();

        var configOption = new Option<string?>("--config", ["-c"]);
        var rootCommand = new RootCommand("Hokai — uptime monitoring daemon and CLI");
        rootCommand.Add(configOption);
        rootCommand.Add(EndpointCommands.Create(stores.endpointStore, stores.checkStore));
        rootCommand.Add(StatusCommand.Create(stores.endpointStore, stores.checkStore));
        rootCommand.Add(ServiceCommands.Create(serviceManager));

        var parseResult = rootCommand.Parse("--config=/etc/hokai/appsettings.json status");

        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void RootCommand_AcceptsConfigShortForm()
    {
        using var temp = new TempDir();
        var stores = CreateStores(temp.Path);
        var serviceManager = new FakeServiceManager();

        var configOption = new Option<string?>("--config", ["-c"]);
        var rootCommand = new RootCommand("Hokai — uptime monitoring daemon and CLI");
        rootCommand.Add(configOption);
        rootCommand.Add(EndpointCommands.Create(stores.endpointStore, stores.checkStore));
        rootCommand.Add(StatusCommand.Create(stores.endpointStore, stores.checkStore));
        rootCommand.Add(ServiceCommands.Create(serviceManager));

        var parseResult = rootCommand.Parse("-c /etc/hokai/appsettings.json status");

        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void RootCommand_RejectsUnknownOption()
    {
        using var temp = new TempDir();
        var stores = CreateStores(temp.Path);
        var serviceManager = new FakeServiceManager();

        var configOption = new Option<string?>("--config", ["-c"]);
        var rootCommand = new RootCommand("Hokai — uptime monitoring daemon and CLI");
        rootCommand.Add(configOption);
        rootCommand.Add(EndpointCommands.Create(stores.endpointStore, stores.checkStore));
        rootCommand.Add(StatusCommand.Create(stores.endpointStore, stores.checkStore));
        rootCommand.Add(ServiceCommands.Create(serviceManager));

        var parseResult = rootCommand.Parse("--unknown status");

        Assert.NotEmpty(parseResult.Errors);
    }

    private static (IEndpointStore endpointStore, ICheckStore checkStore) CreateStores(string dataDir)
    {
        var endpointStore = new EndpointStore(dataDir);
        var checkStore = new CheckStore(dataDir);
        return (endpointStore, checkStore);
    }

    private sealed class FakeServiceManager : IServiceManager
    {
        public Task InstallAsync(CancellationToken ct) => Task.CompletedTask;
        public Task UninstallAsync(bool purge, CancellationToken ct) => Task.CompletedTask;
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<string> GetStatusAsync(CancellationToken ct) => Task.FromResult("not installed");
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
