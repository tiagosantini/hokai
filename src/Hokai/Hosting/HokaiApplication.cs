using Hokai.Commands;
using Hokai.Models;
using Hokai.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

namespace Hokai.Hosting;

public static class HokaiApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        var configPath = ParseBootstrapConfig(args);
        var configDir = Path.GetDirectoryName(Path.GetFullPath(
            configPath ?? "appsettings.json"));

        AppSettings settings;
        if (configPath is not null && File.Exists(configPath))
            settings = AppSettingsLoader.Load(configPath);
        else
            settings = AppSettingsLoader.LoadDefaults();

        var executablePath = Environment.ProcessPath ?? "hokai";
        var execDir = Path.GetDirectoryName(executablePath) ?? ".";
        var resolver = new ConfigurationPathResolver();

        var paths = DetectApplicationPaths(settings);
        var resolvedConfig = resolver.Resolve(
            explicitConfigPath: configPath,
            envConfigPath: Environment.GetEnvironmentVariable("HOKAI_CONFIG_PATH"),
            canonicalConfigExists: File.Exists(paths.ConfigPath),
            executableDirectory: execDir,
            canonicalConfigPath: paths.ConfigPath,
            serviceName: "hokai");

        // Reload settings if resolved path differs
        if (resolvedConfig != configPath && File.Exists(resolvedConfig))
            settings = AppSettingsLoader.Load(resolvedConfig);

        var serviceContext = new ServiceManagerContext
        {
            Paths = paths,
            ExecutablePath = executablePath,
            SudoUserName = Environment.GetEnvironmentVariable("SUDO_USER") ?? "",
            IsElevated = Environment.UserName == "root" ||
                         Environment.UserName == "Administrator"
        };

        var rootCommand = BuildRootCommand(args, settings, serviceContext);

        // If user ran 'run', build the full host with daemon services
        // For CLI commands, build a minimal host (no MonitorService)
        var isRunCommand = args.Length > 0 && args[0] == "run";

        var builder = Host.CreateDefaultBuilder(args);
        builder.ConfigureServices(services =>
        {
            services.AddHokaiCore(settings, serviceContext);
            services.AddHokaiMonitoring();
            if (isRunCommand)
                services.AddHokaiDaemon();
        });

        // Context-aware OS integration — no-op when not in systemd/Windows Service
        builder.UseSystemd();
        builder.UseWindowsService(options =>
        {
            options.ServiceName = "Hokai";
        });

        using var host = builder.Build();

        if (isRunCommand)
        {
            // Validate the saved host is not already registered through sc.exe query equivalent
            await host.RunAsync();
            return 0;
        }

        return await rootCommand.Parse(args).InvokeAsync(
            new InvocationConfiguration(), CancellationToken.None);
    }

    private static string? ParseBootstrapConfig(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--config" || args[i] == "-c")
                return args[i + 1];

            if (args[i].StartsWith("--config="))
                return args[i]["--config=".Length..];

            if (args[i].StartsWith("-c="))
                return args[i][3..];
        }
        return null;
    }

    private static ApplicationPaths DetectApplicationPaths(AppSettings settings)
    {
        if (OperatingSystem.IsLinux())
            return ApplicationPaths.ForLinux("hokai");

        if (OperatingSystem.IsMacOS())
            return ApplicationPaths.ForMacOS(Environment.UserName, "hokai");

        if (OperatingSystem.IsWindows())
            return ApplicationPaths.ForWindows("Hokai");

        return new ApplicationPaths
        {
            ConfigPath = Path.Combine(settings.DataDirectory, "appsettings.json"),
            DataDirectory = settings.DataDirectory,
            ConfigDirectory = settings.DataDirectory
        };
    }

    private static RootCommand BuildRootCommand(
        string[] args,
        AppSettings settings,
        ServiceManagerContext serviceContext)
    {
        var rootCommand = new RootCommand("Hokai — uptime monitoring daemon and CLI");

        // Resolve core services for CLI commands (without MonitorService)
        var services = new ServiceCollection();
        services.AddHokaiCore(settings, serviceContext);
        services.AddHokaiMonitoring();
        var provider = services.BuildServiceProvider();

        var endpointStore = provider.GetRequiredService<IEndpointStore>();
        var checkStore = provider.GetRequiredService<ICheckStore>();
        var serviceManager = provider.GetRequiredService<IServiceManager>();

        rootCommand.Add(EndpointCommands.Create(endpointStore, checkStore));
        rootCommand.Add(StatusCommand.Create(endpointStore, checkStore));
        rootCommand.Add(ServiceCommands.Create(serviceManager));

        // 'run' is handled by the host above, but declare it for help
        var runCommand = new Command("run", "Start the monitoring daemon in foreground");
        rootCommand.Add(runCommand);

        return rootCommand;
    }
}
