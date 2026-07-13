# Hokai — Running as an OS Service

> Implementation plan for running Hokai as a native operating system service (systemd, launchd, Windows Service), with CLI commands for full lifecycle management.

**Related docs**: [Architecture](architecture.md) (core design) | [Installation](installation.md) (install/uninstall)

---

## 1. Design Decisions (settled)

| Decision | Choice |
|---|---|
| OS lifecycle integration | **Microsoft packages** — `Hosting.Systemd` + `Hosting.WindowsServices` |
| Command scope | **Full** — `install`, `uninstall`, `start`, `stop`, `status` |
| Binary installation | **External** — installer scripts place the executable; `service install` only registers the service |

### Updated Dependencies

| Package | Origin | Need |
|---|---|---|
| `System.CommandLine` | NuGet | CLI parsing |
| `Microsoft.Extensions.Http` | NuGet | `IHttpClientFactory`, connection pooling |
| `Microsoft.Extensions.Hosting.Systemd` | NuGet | `sd_notify`, `Type=notify` support, automatic SIGTERM handling |
| `Microsoft.Extensions.Hosting.WindowsServices` | NuGet | Windows Service Control, `Start`, `Stop`, `Shutdown` events |

**Total: 4 NuGet packages (all Microsoft).** No third-party dependencies.

---

## 2. Service Commands

```
hokai service install
hokai service uninstall [--purge]
hokai service start
hokai service stop
hokai service status
```

`hokai run` remains unchanged for foreground execution (dev/debug/manual).

`service uninstall --purge` removes the service registration, configuration, and data directory. Without `--purge`, only the service registration is removed; config and data are preserved for future reinstallation.

---

## 3. Platform-Specific Behavior

### 3.1 Linux — systemd

| Step | What happens |
|---|---|
| `install` | 1. Requests sudo if needed. 2. Creates system group `hokai` and system user `hokai` idempotently. 3. Adds the invoking sudo user to group `hokai` when applicable. 4. Creates data directory `/var/lib/hokai/` with group ownership and `g+rw`. 5. Creates config directory `/etc/hokai/` with group ownership and `g+rw`. 6. Writes default config only if absent. 7. Generates unit file at `/etc/systemd/system/hokai.service`. 8. Runs `systemctl daemon-reload && systemctl enable hokai`. |
| `uninstall` | 1. `systemctl stop hokai && systemctl disable hokai`. 2. Removes `/etc/systemd/system/hokai.service`. 3. With `--purge`: removes `/etc/hokai/` and `/var/lib/hokai/`. |
| `start` | `systemctl start hokai` |
| `stop` | `systemctl stop hokai` |
| `status` | `systemctl is-active hokai` → maps to label |

**Unit file template** (`/etc/systemd/system/hokai.service`):

```ini
[Unit]
Description=Hokai Uptime Monitor
Documentation=https://github.com/tiagosantini/hokai
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=/usr/local/bin/hokai --config /etc/hokai/appsettings.json run
WorkingDirectory=/etc/hokai
User=hokai
Group=hokai
UMask=0002
Restart=on-failure
RestartSec=10s
LimitNOFILE=4096

# Security
NoNewPrivileges=yes
ProtectSystem=strict
ProtectHome=yes
ReadWritePaths=/var/lib/hokai
ReadOnlyPaths=/etc/hokai/appsettings.json

[Install]
WantedBy=multi-user.target
```
The `hokai` system user and group, along with file permissions, are created during `service install`.

### 3.2 macOS — launchd

| Step | What happens |
|---|---|
| `install` | 1. Creates config directory `~/Library/Application Support/Hokai/`. 2. Creates data directory `~/Library/Application Support/Hokai/Data/`. 3. Writes default config only if absent. 4. Generates plist at `~/Library/LaunchAgents/com.hokai.daemon.plist`. Does not start. |
| `uninstall` | 1. `launchctl bootout gui/$UID/com.hokai.daemon` if loaded. 2. Removes plist. 3. With `--purge`: removes config and data directories. |
| `start` | `launchctl bootstrap gui/$UID` if not loaded, then `launchctl kickstart gui/$UID/com.hokai.daemon` |
| `stop` | `launchctl bootout gui/$UID/com.hokai.daemon` |
| `status` | `launchctl print gui/$UID/com.hokai.daemon` → maps to label |

Does not require sudo (user LaunchAgent). Config, data, and definitions stay under the user's Library directory.

**plist template** (`~/Library/LaunchAgents/com.hokai.daemon.plist`):

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.hokai.daemon</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/bin/hokai</string>
        <string>--config</string>
        <string>/Users/[USER]/Library/Application Support/Hokai/appsettings.json</string>
        <string>run</string>
    </array>
    <key>WorkingDirectory</key>
    <string>/Users/[USER]/Library/Application Support/Hokai</string>
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
    <string>/Users/[USER]/Library/Logs/Hokai/stdout.log</string>
    <key>StandardErrorPath</key>
    <string>/Users/[USER]/Library/Logs/Hokai/stderr.log</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ENVIRONMENT</key>
        <string>Production</string>
    </dict>
</dict>
</plist>
```

`[USER]` is replaced with the actual username during `install`. The plist uses `RunAtLoad=false`; the service is started explicitly via `launchctl kickstart`.

### 3.3 Windows — Windows Service

| Step | What happens |
|---|---|
| `install` | 1. Requests privilege elevation (admin). 2. Creates config directory `%ProgramData%\Hokai\`. 3. Creates data directory `%ProgramData%\Hokai\Data\`. 4. Writes default config only if absent. 5. Grants `NT AUTHORITY\LocalService` access to data directory via `icacls`. 6. `sc.exe create Hokai binPath= "..." start= auto obj= "NT AUTHORITY\LocalService"`. |
| `uninstall` | 1. `sc.exe stop Hokai`. 2. `sc.exe delete Hokai`. 3. With `--purge`: removes config and data directories. |
| `start` | `sc.exe start Hokai` |
| `stop` | `sc.exe stop Hokai` |
| `status` | `sc.exe query Hokai` → maps to label |

The executable path is provided by the external installer; `service install` does not copy the binary.

**Install command** (paths resolved at runtime):

```powershell
sc.exe create Hokai `
    binPath= "\"C:\Program Files\Hokai\hokai.exe\" --config \"C:\ProgramData\Hokai\appsettings.json\" run" `
    start= auto `
    obj= "NT AUTHORITY\LocalService" `
    DisplayName= "Hokai Uptime Monitor"
```

- `Hosting.WindowsServices` ensures the process correctly responds to `Start`, `Stop`, and `Shutdown` commands from the Service Control Manager
- The binary must be published as self-contained to avoid requiring an installed runtime
- `icacls` grants the LocalService SID `(OI)(CI)(M)` on the data directory so the service can write check results

---

## 4. Internal Architecture

### 4.1 Files

```text
src/Hokai/
├── Commands/
│   └── ServiceCommands.cs
├── Services/
│   ├── ServiceManager.cs           # facade, selects backend
│   ├── IServiceManagerBackend.cs   # backend contract
│   ├── ServiceManager.Linux.cs     # systemd implementation
│   ├── ServiceManager.MacOS.cs     # launchd implementation
│   └── ServiceManager.Windows.cs   # Windows implementation
└── Hosting/
    ├── ApplicationPaths.cs
    ├── ConfigurationPathResolver.cs
    ├── AppSettingsLoader.cs
    ├── HokaiApplication.cs         # CLI/daemon router
    └── ServiceCollectionExtensions.cs
```

### 4.2 Program.cs (planned)

Uses `Host.CreateDefaultBuilder` to enable both `UseSystemd()` and `UseWindowsService()`:

```csharp
return await HokaiApplication.RunAsync(args);
```

### 4.3 Host Integration

Both `UseSystemd()` and `UseWindowsService()` are context-aware and no-op when not running in the corresponding environment:

```csharp
Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .UseWindowsService(options =>
    {
        options.ServiceName = "Hokai";
    })
```

On Linux, `UseSystemd()` enables `sd_notify` with `Type=notify` and handles `SIGTERM`.
On Windows, `UseWindowsService()` connects to the Service Control Manager.
On macOS, the default `ConsoleLifetime` handles launchd signals.

### 4.4 ServiceManager

Abstraction over each platform's native tools. Responsibilities:

```
ServiceManager
├── InstallAsync(cancellationToken)
│   ├── 1. DetectPlatform()
│   ├── 2. EnsureDirectoriesAsync()              # create config + data dirs
│   ├── 3. WriteDefaultConfigAsync()             # only if absent — never overwrite
│   ├── 4. ApplyPermissionsAsync()               # user, group, ACLs
│   ├── 5. GenerateDefinitionFileAsync()         # platform-specific template
│   ├── 6. WriteDefinitionFileAsync(path)        # write to correct location
│   └── 7. EnableServiceAsync()                  # systemctl enable / sc create
│
├── UninstallAsync(purge, cancellationToken)
│   ├── 1. StopServiceAsync()                    # systemctl stop / launchctl bootout / sc stop
│   ├── 2. DisableServiceAsync()                 # systemctl disable / sc delete
│   ├── 3. RemoveDefinitionFileAsync()           # unit / plist
│   └── 4. If purge: RemoveConfigAndDataAsync()  # config + data dirs
│
├── StartAsync(cancellationToken)
├── StopAsync(cancellationToken)
└── GetStatusAsync(cancellationToken)
```

Binary copying is handled by external installer scripts, not by `ServiceManager`.

### 4.5 ServiceCommands

Already implemented. The `service` command delegates to `IServiceManager` via `System.CommandLine` subcommands. An `uninstall` command accepts `--purge` to remove configuration and data.

### 4.5 Permissions and Elevation

| Platform | Command | Requires Elevation? | Handling |
|---|---|---|---|
| Linux | `install/uninstall` | Yes (sudo) | Detect if not root → show error message with sudo instructions |
| Linux | `start/stop/status` | Depends on systemd policy | Execute directly |
| macOS | all | No (LaunchAgent) | Execute directly |
| Windows | `install/uninstall` | Yes (admin) | Show error message with admin instructions |
| Windows | `start/stop/status` | May require admin | Execute directly; OS policy may prompt |

Elevation strategy:
1. Attempt to run the command directly
2. If it fails with `PermissionDenied` / `AccessDenied`:
   - **Linux**: inform "This command requires root privileges. Run with sudo."
   - **Windows**: inform "This command requires administrator privileges. Run as Administrator."
3. No automatic re-execution with `sudo` / `runas` in the initial release.

---

## 5. File Locations by Platform

Platform file locations are documented in [Installation > What Gets Installed](installation.md#2-what-gets-installed). The table below adds location details specific to the daemon:

| File | Linux | macOS | Windows |
|---|---|---|---|
| Logs | journald (integrated with systemd) | `~/Library/Logs/Hokai/stdout.log` and `~/Library/Logs/Hokai/stderr.log` | Event Log (integrated with Windows Service) |
| Service definition | `/etc/systemd/system/hokai.service` | `~/Library/LaunchAgents/com.hokai.daemon.plist` | Registry (`sc.exe create`) |

---

## 6. Complete User Flow

### Linux

```bash
# Publish the self-contained binary
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true

# Install as a service
sudo hokai service install

# Check status
hokai service status
# Output:
#   Service: active (running)

# Add an endpoint (works regardless of whether the service is running)
hokai endpoint add https://new-api.example.com/health
# The daemon picks up the new endpoint within 30s automatically

# Stop / start
hokai service stop
hokai service start

# Uninstall
sudo hokai service uninstall
```

### macOS

```bash
# Same flow, no sudo (LaunchAgent)
hokai service install
hokai service start
```

### Windows (PowerShell as Admin)

```powershell
hokai service install
hokai service start
hokai service status
```

---

## 7. Pending Decisions

| Question | Impact | Decision |
|---|---|---|---|
| Create `hokai` user on Linux automatically during `install`? | Security vs convenience | Yes — install creates the user and group idempotently. |
| Should `uninstall` remove config and data? | Accidental data loss | Only with `--purge`. Default preserves both. |
| Support `hokai service logs` for log tailing? | Convenience | Future improvement. For now `journalctl -u hokai` on Linux. |
| Support multiple service instances? | Large scope | Out of initial scope. Single-instance only. |
| Should the daemon expose its own HTTP health check? | Monitoring the monitor | Out of initial scope. |

---

## 8. Implementation — Current State

1. **IServiceManager contract** — implemented
2. **ServiceCommands** — implemented
3. **ServiceManager backends** — implemented (systemd, launchd, Windows)
4. **Host bootstrap and DI** — implemented
5. **Program.cs router** — implemented
6. **Templates** — embedded in backend implementations as string constants
