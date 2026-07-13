using Hokai.Hosting;

namespace Hokai.Services;

public sealed class WindowsServiceManager : IServiceManagerBackend
{
    private const string ServiceName = "Hokai";
    private const string ServiceAccount = @"NT AUTHORITY\LocalService";
    internal const int ServiceNotActiveCode = 1062;
    internal const int ServiceAlreadyRunningCode = 1056;

    private readonly ServiceManagerContext _ctx;

    public WindowsServiceManager(ServiceManagerContext context)
    {
        _ctx = context ?? throw new ArgumentNullException(nameof(context));
    }

    public ApplicationPaths Paths => _ctx.Paths;

    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (!_ctx.IsElevated)
            throw new ServiceManagerException(
                "Service installation requires administrator privileges. Run as Administrator.");

        EnsureDirectory(_ctx.Paths.ConfigDirectory);
        EnsureDirectory(_ctx.Paths.DataDirectory);
        EnsureConfigFile();

        var existingService = await RunAllowNonZeroReturnAsync(
            "sc.exe", ["query", ServiceName], cancellationToken);

        await GrantDirectoryAccess(_ctx.Paths.DataDirectory, cancellationToken);

        if (existingService.ExitCode == 0)
        {
            await RunAsync("sc.exe", ["config", ServiceName,
                "binPath=", BuildBinPath(),
                "start=", "auto",
                "obj=", ServiceAccount], cancellationToken);
        }
        else
        {
            await RunAsync("sc.exe", ["create", ServiceName,
                "binPath=", BuildBinPath(),
                "start=", "auto",
                "obj=", ServiceAccount,
                "DisplayName=", "Hokai Uptime Monitor"], cancellationToken);
        }
    }

    public async Task UninstallAsync(bool purge, CancellationToken cancellationToken = default)
    {
        if (!_ctx.IsElevated)
            throw new ServiceManagerException(
                "Service removal requires administrator privileges. Run as Administrator.");

        var stopResult = await RunAsync("sc.exe", ["stop", ServiceName], cancellationToken);
        var stopOk = stopResult.ExitCode == 0 || stopResult.ExitCode == ServiceNotActiveCode;
        var deleteResult = await RunAsync("sc.exe", ["delete", ServiceName], cancellationToken);
        var deleteOk = deleteResult.ExitCode == 0;

        if (purge && stopOk && deleteOk)
        {
            SafeDeleteDirectory(_ctx.Paths.ConfigDirectory);
            SafeDeleteDirectory(_ctx.Paths.DataDirectory);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync("sc.exe", ["start", ServiceName], cancellationToken);
        if (result.ExitCode != 0 && result.ExitCode != ServiceAlreadyRunningCode)
            throw new ServiceManagerException(
                $"Failed to start service (exit code {result.ExitCode}): {result.StandardError}");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync("sc.exe", ["stop", ServiceName], cancellationToken);
        if (result.ExitCode != 0 && result.ExitCode != ServiceNotActiveCode)
            throw new ServiceManagerException(
                $"Failed to stop service (exit code {result.ExitCode}): {result.StandardError}");
    }

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        // Status text parsing depends on sc.exe output format, which varies by OS locale.
        // Known limitation: non-English Windows returns localized state strings that
        // will not match the English literals below and will fall through to the raw line.
        var result = await RunAllowNonZeroReturnAsync(
            "sc.exe", ["query", ServiceName], cancellationToken);

        if (result.ExitCode != 0)
            return "not installed";

        if (result.StandardOutput.Contains("RUNNING"))
            return "running";

        if (result.StandardOutput.Contains("STOPPED"))
            return "stopped";

        if (result.StandardOutput.Contains("STOP_PENDING"))
            return "stop pending";

        if (result.StandardOutput.Contains("START_PENDING"))
            return "start pending";

        return result.StandardOutput.Trim().Split('\n')[^1].Trim();
    }

    private string BuildBinPath()
        => $"\"{_ctx.ExecutablePath}\" --config \"{_ctx.Paths.ConfigPath}\" run";

    private async Task GrantDirectoryAccess(string path, CancellationToken ct)
    {
        var result = await RunAllowNonZeroReturnAsync(
            "icacls.exe", [path, "/grant", $"{ServiceAccount}:(OI)(CI)(M)"], ct);
    }

    private void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private void EnsureConfigFile()
    {
        if (File.Exists(_ctx.Paths.ConfigPath)) return;
        var dir = Path.GetDirectoryName(_ctx.Paths.ConfigPath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(_ctx.Paths.ConfigPath, GetDefaultConfig());
    }

    private async Task<ProcessResult> RunAsync(string exe, string[] args, CancellationToken ct) =>
        await _ctx.ProcessRunner.RunAsync(exe, args, ct);

    private async Task<ProcessResult> RunAllowNonZeroReturnAsync(
        string exe, string[] args, CancellationToken ct)
    {
        try { return await RunAsync(exe, args, ct); }
        catch (OperationCanceledException) { throw; }
        catch { return new ProcessResult { ExitCode = 1, StandardError = "command failed" }; }
    }

    private async Task RunAllowNonZeroAsync(string exe, string[] args, CancellationToken ct)
    {
        try { await RunAsync(exe, args, ct); }
        catch (OperationCanceledException) { throw; }
        catch { }
    }

    private static void SafeDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { }
    }

    private const string DefaultConfig = """
    {
      "Smtp": {
        "Host": "localhost",
        "Port": 25,
        "UseSsl": false,
        "Username": "",
        "Password": "",
        "FromAddress": "hokai@localhost",
        "ToAddresses": []
      },
      "DataDirectory": "__DATADIR__",
      "RetentionDays": 30
    }
    """;

    private static string GetDefaultConfig()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrEmpty(programData))
            programData = @"C:\ProgramData";
        var dataDir = Path.Combine(programData, "Hokai", "Data").Replace("\\", "\\\\");
        return DefaultConfig.Replace("\"__DATADIR__\"", $"\"{dataDir}\"");
    }
}
