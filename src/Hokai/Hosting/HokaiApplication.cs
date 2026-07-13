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
        var configPath = ParseConfigFlag(args);

        AppSettings settings;
        if (configPath is not null)
        {
            if (!File.Exists(configPath))
            {
                await Console.Error.WriteLineAsync(
                    $"Error: configuration file not found: {configPath}");
                return 1;
            }
            settings = AppSettingsLoader.Load(configPath);
        }
        else
        {
            settings = AppSettingsLoader.LoadDefaults();
        }

        var assemblyDir = AppContext.BaseDirectory;
        var resolver = new ConfigurationPathResolver();
        var paths = DetectApplicationPaths();

        var resolvedConfig = resolver.Resolve(
            explicitConfigPath: configPath,
            envConfigPath: Environment.GetEnvironmentVariable("HOKAI_CONFIG_PATH"),
            canonicalConfigExists: File.Exists(paths.ConfigPath),
            executableDirectory: assemblyDir,
            canonicalConfigPath: paths.ConfigPath,
            serviceName: "hokai");

        if (resolvedConfig != configPath && File.Exists(resolvedConfig))
            settings = AppSettingsLoader.Load(resolvedConfig);

        var platform = PlatformContext.Detect();
        var serviceContext = new ServiceManagerContext
        {
            Paths = paths,
            ExecutablePath = platform.ExecutablePath,
            SudoUserName = platform.SudoUserName,
            HomeDirectory = platform.HomeDirectory,
            IsElevated = platform.IsElevated
        };

        var builder = Host.CreateDefaultBuilder(args);
        builder.ConfigureServices(services =>
        {
            services.AddHokaiCore(settings, serviceContext);
            services.AddHokaiMonitoring();
            services.AddHokaiDaemon();
        });

        builder.UseSystemd();
        builder.UseWindowsService(options => { options.ServiceName = "Hokai"; });

        using var host = builder.Build();

        // Wire up CLI commands from the host's provider
        var endpointStore = host.Services.GetRequiredService<IEndpointStore>();
        var checkStore = host.Services.GetRequiredService<ICheckStore>();
        var serviceManager = host.Services.GetRequiredService<IServiceManager>();

        var rootCommand = new RootCommand("Hokai — uptime monitoring daemon and CLI");
        rootCommand.Add(EndpointCommands.Create(endpointStore, checkStore));
        rootCommand.Add(StatusCommand.Create(endpointStore, checkStore));
        rootCommand.Add(ServiceCommands.Create(serviceManager));

        var runCommand = new Command("run", "Start the monitoring daemon in foreground");
        runCommand.SetAction(async (ParseResult _, CancellationToken ct) =>
        {
            await host.RunAsync(ct);
        });
        rootCommand.Add(runCommand);

        return await rootCommand.Parse(args).InvokeAsync(
            new InvocationConfiguration(), CancellationToken.None);
    }

    /// <summary>
    /// Scans raw args for --config value, --config=value, -c value, or -c=value.
    /// Returns the path or null. Throws if --config/-c has no value.
    /// </summary>
    internal static string? ParseConfigFlag(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" || args[i] == "-c")
            {
                if (i + 1 >= args.Length)
                    throw new InvalidOperationException(
                        $"Missing value for {args[i]}. Expected a file path.");
                return args[i + 1];
            }

            if (args[i].StartsWith("--config=", StringComparison.Ordinal))
                return args[i]["--config=".Length..];

            if (args[i].StartsWith("-c=", StringComparison.Ordinal))
                return args[i][3..];
        }

        return null;
    }

    private static ApplicationPaths DetectApplicationPaths()
    {
        if (OperatingSystem.IsLinux())
            return ApplicationPaths.ForLinux("hokai");
        if (OperatingSystem.IsMacOS())
            return ApplicationPaths.ForMacOS(Environment.UserName, "hokai");
        if (OperatingSystem.IsWindows())
            return ApplicationPaths.ForWindows("Hokai");

        return new ApplicationPaths
        {
            ConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            DataDirectory = Path.Combine(AppContext.BaseDirectory, "Data"),
            ConfigDirectory = AppContext.BaseDirectory
        };
    }
}
