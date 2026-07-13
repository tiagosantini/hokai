using Hokai.Hosting;

namespace Hokai.Services;

public sealed class LaunchdServiceManager : IServiceManagerBackend
{
    private readonly ServiceManagerContext _ctx;

    public LaunchdServiceManager(ServiceManagerContext context)
    {
        _ctx = context ?? throw new ArgumentNullException(nameof(context));
    }

    public ApplicationPaths Paths => _ctx.Paths;

    public Task InstallAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectory(_ctx.Paths.ConfigDirectory);
        EnsureDirectory(_ctx.Paths.DataDirectory);
        EnsureConfigFile();
        WritePlistFile();
        return Task.CompletedTask;
    }

    public async Task UninstallAsync(bool purge, CancellationToken cancellationToken = default)
    {
        await RunAllowNonZeroAsync("launchctl", ["bootout", $"gui/{GetUid()}/{Label}"], cancellationToken);
        File.Delete(_ctx.Paths.DefinitionPath);

        if (purge)
        {
            SafeDeleteDirectory(_ctx.Paths.ConfigDirectory);
            SafeDeleteDirectory(_ctx.Paths.DataDirectory);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // First check if it's already loaded
        var listResult = await RunAllowNonZeroReturnAsync(
            "launchctl", ["print", $"gui/{GetUid()}/{Label}"], cancellationToken);

        // If not loaded, bootstrap first
        if (listResult.ExitCode != 0 || listResult.StandardError.Contains("not found"))
        {
            await RunAsync("launchctl", ["bootstrap", $"gui/{GetUid()}", _ctx.Paths.DefinitionPath], cancellationToken);
        }

        await RunAsync("launchctl", ["kickstart", "-p", $"gui/{GetUid()}/{Label}"], cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await RunAllowNonZeroAsync("launchctl", ["bootout", $"gui/{GetUid()}/{Label}"], cancellationToken);
    }

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_ctx.Paths.DefinitionPath))
            return "not installed";

        var result = await RunAllowNonZeroReturnAsync(
            "launchctl", ["print", $"gui/{GetUid()}/{Label}"], cancellationToken);

        if (result.ExitCode != 0 || result.StandardError.Contains("not found"))
            return "installed (stopped)";

        return "active (running)";
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

        File.WriteAllText(_ctx.Paths.ConfigPath, DefaultConfig);
    }

    private void WritePlistFile()
    {
        var dir = Path.GetDirectoryName(_ctx.Paths.DefinitionPath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(_ctx.Paths.DefinitionPath, GeneratePlist());
    }

    private string GeneratePlist()
    {
        var userName = _ctx.SudoUserName;
        if (string.IsNullOrEmpty(userName))
            userName = Environment.UserName;

        var stdoutDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            "..", "Library", "Logs", "Hokai");
        Directory.CreateDirectory(stdoutDir);

        return string.Create(null,
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
              "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>com.hokai.daemon</string>
                <key>ProgramArguments</key>
                <array>
                    <string>{EscapeXml(_ctx.ExecutablePath)}</string>
                    <string>--config</string>
                    <string>{EscapeXml(_ctx.Paths.ConfigPath)}</string>
                    <string>run</string>
                </array>
                <key>WorkingDirectory</key>
                <string>{EscapeXml(_ctx.Paths.ConfigDirectory)}</string>
                <key>RunAtLoad</key>
                <false/>
                <key>KeepAlive</key>
                <dict>
                    <key>SuccessfulExit</key>
                    <false/>
                </dict>
                <key>ThrottleInterval</key>
                <integer>10</integer>
                <key>StandardOutPath</key>
                <string>{EscapeXml(Path.Combine(stdoutDir, "stdout.log"))}</string>
                <key>StandardErrorPath</key>
                <string>{EscapeXml(Path.Combine(stdoutDir, "stderr.log"))}</string>
                <key>EnvironmentVariables</key>
                <dict>
                    <key>DOTNET_ENVIRONMENT</key>
                    <string>Production</string>
                </dict>
            </dict>
            </plist>
            """);
    }

    private static string EscapeXml(string value) =>
        System.Net.WebUtility.HtmlEncode(value);

    private string Label => "com.hokai.daemon";

    private string GetUid()
    {
        try
        {
            var result = _ctx.ProcessRunner.RunAsync("id", ["-u"], CancellationToken.None)
                .GetAwaiter().GetResult();
            if (result.ExitCode == 0)
                return result.StandardOutput.Trim();
        }
        catch { }
        return "501";
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
      "DataDirectory": "Data",
      "RetentionDays": 30
    }
    """;
}
