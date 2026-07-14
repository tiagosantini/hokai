using Hokai.Hosting;

namespace Hokai.Services;

public sealed class SystemdServiceManager : IServiceManagerBackend
{
    private readonly ServiceManagerContext _ctx;

    public SystemdServiceManager(ServiceManagerContext context)
    {
        _ctx = context ?? throw new ArgumentNullException(nameof(context));
    }

    public ApplicationPaths Paths => _ctx.Paths;

    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (!_ctx.IsElevated)
            throw new ServiceManagerException(
                "Service installation requires root privileges. Run with sudo.");

        EnsureSystemGroup("hokai", cancellationToken);
        EnsureSystemUser("hokai", cancellationToken);
        AddUserToGroup("hokai", cancellationToken);
        EnsureDirectory(_ctx.Paths.DataDirectory, "hokai", "hokai", cancellationToken);
        EnsureDirectory(_ctx.Paths.ConfigDirectory, "hokai", "hokai", cancellationToken);
        EnsureConfigFile(cancellationToken);
        WriteUnitFile(cancellationToken);

        var reloadResult = await RunAsync("systemctl", ["daemon-reload"], cancellationToken);
        if (reloadResult.ExitCode != 0)
            throw new ServiceManagerException(
                $"systemctl daemon-reload failed (exit code {reloadResult.ExitCode}): {reloadResult.StandardError}");

        var enableResult = await RunAsync("systemctl", ["enable", "hokai"], cancellationToken);
        if (enableResult.ExitCode != 0)
            throw new ServiceManagerException(
                $"systemctl enable failed (exit code {enableResult.ExitCode}): {enableResult.StandardError}");
    }

    public async Task UninstallAsync(bool purge, CancellationToken cancellationToken = default)
    {
        if (!_ctx.IsElevated)
            throw new ServiceManagerException(
                "Service removal requires root privileges. Run with sudo.");

        var stopResult = await RunAsync("systemctl", ["stop", "hokai"], cancellationToken);
        if (stopResult.ExitCode != 0)
            throw new ServiceManagerException(
                $"systemctl stop failed (exit code {stopResult.ExitCode}): {stopResult.StandardError}");

        var disableResult = await RunAsync("systemctl", ["disable", "hokai"], cancellationToken);
        if (disableResult.ExitCode != 0)
            throw new ServiceManagerException(
                $"systemctl disable failed (exit code {disableResult.ExitCode}): {disableResult.StandardError}");

        File.Delete(_ctx.Paths.DefinitionPath);

        var reloadResult = await RunAsync("systemctl", ["daemon-reload"], cancellationToken);
        if (reloadResult.ExitCode != 0)
            throw new ServiceManagerException(
                $"systemctl daemon-reload failed (exit code {reloadResult.ExitCode}): {reloadResult.StandardError}");

        if (purge)
        {
            SafeDeleteDirectory(_ctx.Paths.ConfigDirectory);
            SafeDeleteDirectory(_ctx.Paths.DataDirectory);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync("systemctl", ["start", "hokai"], cancellationToken);
        if (result.ExitCode != 0)
            throw new ServiceManagerException(
                $"Failed to start service (exit code {result.ExitCode}): {result.StandardError}");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync("systemctl", ["stop", "hokai"], cancellationToken);
        if (result.ExitCode != 0)
            throw new ServiceManagerException(
                $"Failed to stop service (exit code {result.ExitCode}): {result.StandardError}");
    }

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_ctx.Paths.DefinitionPath))
            return "not installed";

        var result = await RunAsync("systemctl", ["is-active", "hokai"], cancellationToken);
        return result.ExitCode == 0
            ? $"active ({result.StandardOutput.Trim()})"
            : $"inactive ({result.StandardOutput.Trim()})";
    }

    private void EnsureSystemGroup(string group, CancellationToken ct)
    {
        var result = RunAsync("getent", ["group", group], ct).GetAwaiter().GetResult();
        if (result.ExitCode == 0) return;

        RunAndCheck("groupadd", ["--system", group],
            $"Failed to create system group '{group}'.", ct);
    }

    private void EnsureSystemUser(string user, CancellationToken ct)
    {
        var result = RunAsync("id", [user], ct).GetAwaiter().GetResult();
        if (result.ExitCode == 0) return;

        RunAndCheck("useradd", ["--system", "--no-create-home", "--gid", "hokai", "hokai"],
            $"Failed to create system user '{user}'.", ct);
    }

    private void AddUserToGroup(string group, CancellationToken ct)
    {
        var sudoUser = _ctx.SudoUserName;
        if (string.IsNullOrEmpty(sudoUser)) return;

        var result = RunAsync("id", ["-nG", sudoUser], ct).GetAwaiter().GetResult();
        if (result.ExitCode != 0) return;

        var groups = result.StandardOutput.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (groups.Contains(group, StringComparer.Ordinal)) return;

        RunAndCheck("usermod", ["-aG", group, sudoUser],
            $"Failed to add user '{sudoUser}' to group '{group}'.", ct);
    }

    private void EnsureDirectory(string path, string user, string group, CancellationToken ct)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        RunAllowNonZeroAsync("chown", [$"{user}:{group}", path], ct)
            .GetAwaiter().GetResult();
        RunAllowNonZeroAsync("chmod", ["g+rw", path], ct)
            .GetAwaiter().GetResult();
        if (path == _ctx.Paths.DataDirectory)
            RunAllowNonZeroAsync("chmod", ["g+s", path], ct)
                .GetAwaiter().GetResult();
    }

    private void EnsureConfigFile(CancellationToken ct)
    {
        if (File.Exists(_ctx.Paths.ConfigPath)) return;

        var dir = Path.GetDirectoryName(_ctx.Paths.ConfigPath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(_ctx.Paths.ConfigPath, DefaultConfig);
        // Config may contain SMTP credentials — restrict to owner only.
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_ctx.Paths.ConfigPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private void WriteUnitFile(CancellationToken ct)
    {
        var unit = GenerateUnitFile();
        var dir = Path.GetDirectoryName(_ctx.Paths.DefinitionPath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(_ctx.Paths.DefinitionPath, unit);
    }

    private string GenerateUnitFile()
    {
        return string.Create(null,
            $"""
            [Unit]
            Description=Hokai Uptime Monitor
            After=network-online.target
            Wants=network-online.target

            [Service]
            Type=notify
            ExecStart={_ctx.ExecutablePath} --config {_ctx.Paths.ConfigPath} run
            WorkingDirectory={_ctx.Paths.ConfigDirectory}
            User=hokai
            Group=hokai
            UMask=0002
            Restart=on-failure
            RestartSec=10s
            LimitNOFILE=4096
            NoNewPrivileges=yes
            ProtectSystem=strict
            ProtectHome=yes
            ReadWritePaths={_ctx.Paths.DataDirectory}

            [Install]
            WantedBy=multi-user.target
            """);
    }

    private static void SafeDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { /* best-effort */ }
    }

    private async Task<ProcessResult> RunAsync(string exe, string[] args, CancellationToken ct)
    {
        var result = await _ctx.ProcessRunner.RunAsync(exe, args, ct);
        return result;
    }

    private async Task RunAllowNonZeroAsync(string exe, string[] args, CancellationToken ct)
    {
        try { await RunAsync(exe, args, ct); }
        catch (OperationCanceledException) { throw; }
        catch { /* idempotent — ignore failures */ }
    }

    private void RunAndCheck(string exe, string[] args, string errorMessage, CancellationToken ct)
    {
        var result = RunAsync(exe, args, ct).GetAwaiter().GetResult();
        if (result.ExitCode != 0)
            throw new ServiceManagerException($"{errorMessage} {result.StandardError}".Trim());
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
      "DataDirectory": "/var/lib/hokai",
      "RetentionDays": 30
    }
    """;
}
