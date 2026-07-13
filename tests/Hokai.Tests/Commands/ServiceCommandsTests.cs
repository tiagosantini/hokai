using Hokai.Commands;
using Hokai.Services;
using System.CommandLine;

namespace Hokai.Tests.Commands;

[Collection(nameof(ConsoleRedirectCollection))]
public sealed class ServiceCommandsTests
{
    [Fact]
    public async Task InstallCommand_Success_PrintsMessage()
    {
        var manager = new FakeServiceManager();
        var command = ServiceCommands.Create(manager);
        var (exitCode, output, _) = await InvokeAsync(command, "install");

        Assert.Equal(0, exitCode);
        Assert.Contains("installed", output, StringComparison.OrdinalIgnoreCase);
        Assert.True(manager.InstallCalled);
    }

    [Fact]
    public async Task InstallCommand_Failure_ReturnsError()
    {
        var manager = new FakeServiceManager(throwOnInstall: true);
        var command = ServiceCommands.Create(manager);
        var (exitCode, _, error) = await InvokeAsync(command, "install");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Failed", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UninstallCommand_Success_PrintsMessage()
    {
        var manager = new FakeServiceManager();
        var command = ServiceCommands.Create(manager);
        var (exitCode, output, _) = await InvokeAsync(command, "uninstall");

        Assert.Equal(0, exitCode);
        Assert.Contains("uninstalled", output, StringComparison.OrdinalIgnoreCase);
        Assert.True(manager.UninstallCalled);
    }

    [Fact]
    public async Task StartCommand_Success_PrintsMessage()
    {
        var manager = new FakeServiceManager();
        var command = ServiceCommands.Create(manager);
        var (exitCode, output, _) = await InvokeAsync(command, "start");

        Assert.Equal(0, exitCode);
        Assert.Contains("started", output, StringComparison.OrdinalIgnoreCase);
        Assert.True(manager.StartCalled);
    }

    [Fact]
    public async Task StartCommand_Failure_ReturnsError()
    {
        var manager = new FakeServiceManager(throwOnStart: true);
        var command = ServiceCommands.Create(manager);
        var (exitCode, _, error) = await InvokeAsync(command, "start");

        Assert.NotEqual(0, exitCode);
        Assert.NotEmpty(error);
    }

    [Fact]
    public async Task StopCommand_Success_PrintsMessage()
    {
        var manager = new FakeServiceManager();
        var command = ServiceCommands.Create(manager);
        var (exitCode, output, _) = await InvokeAsync(command, "stop");

        Assert.Equal(0, exitCode);
        Assert.Contains("stopped", output, StringComparison.OrdinalIgnoreCase);
        Assert.True(manager.StopCalled);
    }

    [Fact]
    public async Task StatusCommand_PrintsServiceStatus()
    {
        var manager = new FakeServiceManager { Status = "active (running)" };
        var command = ServiceCommands.Create(manager);
        var (exitCode, output, _) = await InvokeAsync(command, "status");

        Assert.Equal(0, exitCode);
        Assert.Contains("active (running)", output);
    }

    [Fact]
    public async Task UnknownSubcommand_ReturnsError()
    {
        var manager = new FakeServiceManager();
        var command = ServiceCommands.Create(manager);
        var (exitCode, _, error) = await InvokeAsync(command, "bogus");

        Assert.NotEqual(0, exitCode);
        Assert.NotEmpty(error);
    }

    private static async Task<(int ExitCode, string Output, string Error)> InvokeAsync(Command command, string args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var parseResult = command.Parse(args);
            var exitCode = await parseResult.InvokeAsync(
                new InvocationConfiguration(),
                CancellationToken.None);
            return (exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed class FakeServiceManager : IServiceManager
    {
        private readonly bool _throwOnInstall;
        private readonly bool _throwOnStart;

        public FakeServiceManager(bool throwOnInstall = false, bool throwOnStart = false)
        {
            _throwOnInstall = throwOnInstall;
            _throwOnStart = throwOnStart;
        }

        public string Status { get; set; } = "not installed";

        public bool InstallCalled { get; private set; }
        public bool UninstallCalled { get; private set; }
        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }

        public Task InstallAsync(CancellationToken ct = default)
        {
            InstallCalled = true;
            if (_throwOnInstall)
                throw new InvalidOperationException("Install failed");
            return Task.CompletedTask;
        }

        public Task UninstallAsync(CancellationToken ct = default)
        {
            UninstallCalled = true;
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken ct = default)
        {
            StartCalled = true;
            if (_throwOnStart)
                throw new InvalidOperationException("Start failed");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public Task<string> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(Status);
    }
}
