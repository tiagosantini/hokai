# Hokai — Running as an OS Service

> Implementation plan for running Hokai as a native operating system service (systemd, launchd, Windows Service), with CLI commands for full lifecycle management.

**Related docs**: [Architecture](architecture.md) (core design) | [Installation](installation.md) (install/uninstall)

---

## 1. Design Decisions (settled)

| Decision | Choice |
|---|---|
| OS lifecycle integration | **Microsoft packages** — `Hosting.Systemd` + `Hosting.WindowsServices` |
| Command scope | **Full** — `install`, `uninstall`, `start`, `stop`, `status` |
| Binary installation | **Auto-copy** — `install` copies the binary to the standard OS path |

### Updated Dependencies

| Package | Origin | Need |
|---|---|---|
| `System.CommandLine` | NuGet | CLI |
| `Microsoft.Extensions.Hosting.Systemd` | NuGet | `sd_notify`, `Type=notify` support, automatic SIGTERM handling |
| `Microsoft.Extensions.Hosting.WindowsServices` | NuGet | Windows Service Control, `Start`, `Stop`, `Shutdown` events |

**Total: 3 NuGet packages (all Microsoft).** No third-party dependencies. For the full dependency breakdown of the core application, see [Architecture > Detailed Dependencies](architecture.md#9-detailed-dependencies).

---

## 2. Service Commands

```
hokai service install   [--config <path>] [--data-dir <path>]
hokai service uninstall
hokai service start
hokai service stop
hokai service status
```

`hokai run` remains unchanged for foreground execution (dev/debug/manual).

---

## 3. Platform-Specific Behavior

### 3.1 Linux — systemd

| Step | What happens |
|---|---|
| `install` | 1. Requests sudo if needed. 2. Copies binary to `/usr/local/bin/hokai`. 3. Creates data directory `/var/lib/hokai/`. 4. Generates unit file at `/etc/systemd/system/hokai.service`. 5. Runs `systemctl daemon-reload && systemctl enable hokai`. |
| `uninstall` | 1. `systemctl stop hokai && systemctl disable hokai`. 2. Removes `/etc/systemd/system/hokai.service`. 3. Removes `/usr/local/bin/hokai`. 4. Asks whether to remove data directory. |
| `start` | `systemctl start hokai` |
| `stop` | `systemctl stop hokai` |
| `status` | `systemctl status hokai` + displays endpoint uptime % |

**Unit file template** (`/etc/systemd/system/hokai.service`):

```ini
[Unit]
Description=Hokai Uptime Monitor
Documentation=https://github.com/user/hokai
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=/usr/local/bin/hokai run
WorkingDirectory=/etc/hokai
Restart=on-failure
RestartSec=10s
User=hokai
Group=hokai
LimitNOFILE=4096

# Security
NoNewPrivileges=yes
ProtectSystem=strict
ProtectHome=yes
ReadWritePaths=/var/lib/hokai /etc/hokai
ReadOnlyPaths=/usr/local/bin/hokai

[Install]
WantedBy=multi-user.target
```

- `Type=notify` — uses `sd_notify` via `Hosting.Systemd` to signal when the service is ready
- `WorkingDirectory=/etc/hokai` — points to where `appsettings.json` resides
- `ProtectSystem=strict` + `ReadWritePaths` — security hardening
- Creating the `hokai` user is the admin's responsibility (or the `install` script with `--create-user` flag)

### 3.2 macOS — launchd

| Step | What happens |
|---|---|
| `install` | 1. Copies binary to `/usr/local/bin/hokai`. 2. Creates data directory `~/Library/Application Support/hokai/`. 3. Generates plist at `~/Library/LaunchAgents/com.hokai.daemon.plist`. 4. `launchctl load` + `launchctl start`. |
| `uninstall` | 1. `launchctl unload`. 2. Removes plist. 3. Removes binary. |
| `start` | `launchctl start com.hokai.daemon` |
| `stop` | `launchctl stop com.hokai.daemon` |
| `status` | `launchctl list com.hokai.daemon` |

Does not require sudo (user LaunchAgent, not a system Daemon).

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
        <string>run</string>
    </array>
    <key>WorkingDirectory</key>
    <string>/usr/local/var/hokai</string>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <dict>
        <key>SuccessfulExit</key>
        <false/>
    </dict>
    <key>ThrottleInterval</key>
    <integer>10</integer>
    <key>StandardOutPath</key>
    <string>/usr/local/var/log/hokai.log</string>
    <key>StandardErrorPath</key>
    <string>/usr/local/var/log/hokai.err</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ENVIRONMENT</key>
        <string>Production</string>
    </dict>
</dict>
</plist>
```

### 3.3 Windows — Windows Service

| Step | What happens |
|---|---|
| `install` | 1. Requests privilege elevation (admin). 2. Copies binary to `%ProgramFiles%\Hokai\hokai.exe`. 3. Creates data directory `%ProgramData%\Hokai\`. 4. `sc.exe create Hokai binPath= "..." start= auto`. |
| `uninstall` | 1. `sc.exe stop Hokai`. 2. `sc.exe delete Hokai`. 3. Removes binary. |
| `start` | `sc.exe start Hokai` |
| `stop` | `sc.exe stop Hokai` |
| `status` | `sc.exe query Hokai` |

**Install command**:

```powershell
sc.exe create Hokai `
    binPath= "\"C:\Program Files\Hokai\hokai.exe\" run" `
    start= auto `
    DisplayName= "Hokai Uptime Monitor"
```

- `Hosting.WindowsServices` ensures the process correctly responds to `Start`, `Stop`, and `Shutdown` commands from the Service Control Manager
- The binary must be published as self-contained to avoid requiring an installed runtime

---

## 4. Internal Architecture

### 4.1 New files

Two files are added to the [canonical project structure](architecture.md#3-project-structure):

```
src/Hokai/
├── Commands/
│   └── ServiceCommands.cs          # NEW
└── Services/
    └── ServiceManager.cs           # NEW
```

### 4.2 Program.cs (updated)

Service environment detection happens during host bootstrap:

```csharp
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// --- Monitoring services ---
builder.Services.AddHostedService<MonitorService>();
builder.Services.AddSingleton<HealthCheckService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<EndpointStore>();
builder.Services.AddSingleton<CheckStore>();

// --- OS service integration ---
if (OperatingSystem.IsLinux())
    builder.Services.AddHostedService<SystemdHostedService>();  // or similar

IHost host = builder.Build();
await host.RunAsync();
```

> **Note**: The exact API for `UseSystemd()` / `UseWindowsService()` depends on the overload available in .NET 10. It may use `Host.CreateDefaultBuilder(args).UseSystemd().UseWindowsService()` instead of `Host.CreateApplicationBuilder`. Implementation detail to be resolved during build.

### 4.3 ServiceManager

Abstraction over each platform's native tools. Responsibilities:

```
ServiceManager
├── InstallAsync(config)
│   ├── 1. DetectPlatform()
│   ├── 2. CopyBinaryAsync(targetPath)        # auto-copy
│   ├── 3. EnsureDataDirectoryAsync(dataDir)   # create data directory
│   ├── 4. GenerateDefinitionFileAsync()        # platform-specific template
│   ├── 5. WriteDefinitionFileAsync(path)       # write to correct location
│   └── 6. EnableServiceAsync()                 # systemctl enable / launchctl load / sc create
│
├── UninstallAsync()
│   ├── 1. DisableServiceAsync()                # systemctl disable / launchctl unload / sc delete
│   ├── 2. RemoveDefinitionFileAsync()
│   └── 3. RemoveBinaryAsync() + prompt remove data?
│
├── StartAsync()
├── StopAsync()
└── GetStatusAsync()
```

### 4.4 ServiceCommands

Integration with `System.CommandLine`:

```csharp
var serviceCommand = new Command("service", "Manage the Hokai background service");

var installCommand = new Command("install", "Install as an OS service");
installCommand.SetHandler(async (configPath, dataDir) => {
    var manager = new ServiceManager();
    await manager.InstallAsync(new ServiceConfig { ... });
});

serviceCommand.AddCommand(installCommand);
// ... uninstall, start, stop, status
```

### 4.5 Permissions and Elevation

| Platform | Command | Requires Elevation? | Handling |
|---|---|---|---|
| Linux | `install` | Yes (sudo) | Detects if not root → re-execute with `sudo` or show error message |
| Linux | `uninstall` | Yes | Same handling |
| Linux | `start/stop/status` | Depends on systemd policy (usually not) | Execute directly |
| macOS | `install/uninstall` | No (LaunchAgent) | Execute directly |
| Windows | `install/uninstall` | Yes (admin) | Detect → `RunAs` or error message |
| Windows | `start/stop/status` | Yes | Same handling |

Elevation strategy:
1. Attempt to run the command directly
2. If it fails with `PermissionDenied` / `AccessDenied`:
   - **Linux/macOS**: inform "This command requires administrative privileges. Run with sudo."
   - **Windows**: inform "This command requires administrator privileges. Run as Administrator."

More convenient alternative (future): detect a non-elevated environment and automatically re-execute with `sudo` / `runas`.

---

## 5. File Locations by Platform

Platform file locations are documented in [Installation > What Gets Installed](installation.md#2-what-gets-installed). The table below adds location details specific to the daemon:

| File | Linux | macOS | Windows |
|---|---|---|---|
| Logs | journald (integrated with systemd) | `/usr/local/var/log/hokai.log` | Event Log (integrated with Windows Service) |
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
#   Uptime (24h): https://api.example.com = 99.97% | https://app.example.com = 100%
#   Data directory: /var/lib/hokai

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

| Question | Impact | Suggestion |
|---|---|---|
| Create `hokai` user on Linux automatically during `install`? | Security vs convenience | `--create-user` flag. Without it, use `User=nobody` as fallback. |
| Should `uninstall` prompt before removing the data directory? | Accidental data loss | Yes, interactive prompt with `--force` to skip. |
| Support `hokai service logs` for log tailing? | Convenience | Yes, future improvement. For now `journalctl -u hokai` on Linux. |
| Support multiple service instances (e.g. `hokai@dev`, `hokai@prod`)? | Large scope | Out of initial scope. Single-instance only. |
| Should the daemon expose its own HTTP health check (e.g. `http://localhost:9090/health`)? | Monitoring the monitor | Out of initial scope. Listed as future improvement. |

---

## 8. Implementation — Suggested Order

1. **ServiceManager** — platform abstraction with 3 backends (Linux, macOS, Windows)
2. **ServiceCommands** — integration with System.CommandLine
3. **Program.cs update** — add `builder.UseSystemd()` / `builder.UseWindowsService()`
4. **Templates** — unit files / plist / sc.exe scripts as embedded resources or strings
5. **Manual tests** — validate `install → start → status → stop → uninstall` on each platform
6. **Elevation** — permission handling and error messages
