using Hokai.Services;
using System.CommandLine;

namespace Hokai.Commands;

public static class ServiceCommands
{
    public static Command Create(IServiceManager serviceManager)
    {
        var command = new Command("service", "Manage the OS service installation and lifecycle");
        command.Add(CreateInstallCommand(serviceManager));
        command.Add(CreateUninstallCommand(serviceManager));
        command.Add(CreateStartCommand(serviceManager));
        command.Add(CreateStopCommand(serviceManager));
        command.Add(CreateStatusCommand(serviceManager));
        return command;
    }

    private static Command CreateInstallCommand(IServiceManager manager)
    {
        var command = new Command("install", "Install the application as an OS service");

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            try
            {
                await manager.InstallAsync(cancellationToken);
                await Console.Out.WriteLineAsync("Service installed successfully.");
                return 0;
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync($"Failed to install service: {exception.Message}");
                return 1;
            }
        });

        return command;
    }

    private static Command CreateUninstallCommand(IServiceManager manager)
    {
        var command = new Command("uninstall", "Remove the OS service registration");

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            try
            {
                await manager.UninstallAsync(cancellationToken);
                await Console.Out.WriteLineAsync("Service uninstalled successfully.");
                return 0;
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync($"Failed to uninstall service: {exception.Message}");
                return 1;
            }
        });

        return command;
    }

    private static Command CreateStartCommand(IServiceManager manager)
    {
        var command = new Command("start", "Start the installed OS service");

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            try
            {
                await manager.StartAsync(cancellationToken);
                await Console.Out.WriteLineAsync("Service started successfully.");
                return 0;
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync($"Failed to start service: {exception.Message}");
                return 1;
            }
        });

        return command;
    }

    private static Command CreateStopCommand(IServiceManager manager)
    {
        var command = new Command("stop", "Stop the running OS service");

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            try
            {
                await manager.StopAsync(cancellationToken);
                await Console.Out.WriteLineAsync("Service stopped successfully.");
                return 0;
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync($"Failed to stop service: {exception.Message}");
                return 1;
            }
        });

        return command;
    }

    private static Command CreateStatusCommand(IServiceManager manager)
    {
        var command = new Command("status", "Show the current service state");

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var status = await manager.GetStatusAsync(cancellationToken);
            await Console.Out.WriteLineAsync(status);
        });

        return command;
    }
}
