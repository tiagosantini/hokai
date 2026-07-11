# Hokai — Uptime Monitoring CLI & Daemon

> Portable, cross-platform uptime monitoring via CLI, with email notifications and JSON persistence. Built with .NET 10 with minimal dependencies.

**Related docs**: [Daemonization](daemonization.md) | [Installation](installation.md)

---

## 1. Overview

Hokai is a background uptime monitoring tool. Users configure HTTP/HTTPS endpoints via CLI, and a daemon performs periodic health checks, calculates uptime percentage (24h window), and sends email notifications on downtime or recovery.

### Design Principles

- **Minimal dependencies** — only NuGet from Microsoft (see [Daemonization > Dependencies](daemonization.md#1-design-decisions-settled) for the complete list)
- **Portable** — single binary, no IPC, no OS-specific services
- **Offline-first operation** — CLI and daemon communicate exclusively through the file system

---

## 2. Tech Stack

| Component | Technology | Origin |
|---|---|---|
| Runtime | .NET 10 | SDK |
| CLI Parser | `System.CommandLine` | NuGet (Microsoft) |
| Host / DI | `Microsoft.Extensions.Hosting` | SDK |
| HTTP Client | `System.Net.Http` (IHttpClientFactory) | SDK |
| SMTP | `System.Net.Mail` (SmtpClient) | SDK |
| Serialization | `System.Text.Json` | SDK |
| Timer | `System.Threading.PeriodicTimer` | SDK |
| Config | `Microsoft.Extensions.Configuration` | SDK |
| Logging | `Microsoft.Extensions.Logging` | SDK |

**Total: 1 external dependency for the core application.** For OS service integration, 2 additional Microsoft packages are required — see [Daemonization > Dependencies](daemonization.md#1-design-decisions-settled).

---

## 3. Project Structure

```
hokai/
├── hokai.sln
├── src/
│   └── Hokai/
│       ├── Hokai.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Commands/
│       │   ├── EndpointCommands.cs
│       │   └── ServiceCommands.cs       # service install/start/stop
│       ├── Models/
│       │   ├── EndpointConfig.cs
│       │   ├── CheckResult.cs
│       │   └── SmtpSettings.cs
│       └── Services/
│           ├── MonitorService.cs
│           ├── HealthCheckService.cs
│           ├── NotificationService.cs
│           ├── EndpointStore.cs
│           ├── CheckStore.cs
│           └── ServiceManager.cs        # platform service abstraction
├── scripts/                              # installer scripts
├── .github/                               # PR template, CI workflows
└── .docs/                                 # design documents
    ├── architecture.md
    ├── daemonization.md
    └── installation.md
```
*The full project tree including installer scripts, Docker, and CI files is in [Installation > Project Structure](installation.md#9-project-file-structure).*
---

## 4. CLI Commands

### `hokai endpoint add <url>`
Adds an endpoint for monitoring.

```
hokai endpoint add https://api.example.com/health \
    --interval 5m \
    --timeout 30s \
    --method GET \
    --expect 200
```

### `hokai endpoint list`
Lists all configured endpoints and their uptime % over the last 24h.

```
hokai endpoint list
```

### `hokai endpoint remove <id>`
Removes an endpoint by its ID (obtained from `list`).

```
hokai endpoint remove abc123
```

### `hokai run`
Starts the monitoring daemon in foreground. For background execution as an OS service see [Daemonization](daemonization.md).

```
hokai run
```

### `hokai status`
Shows the current status of all endpoints: last check, response time, and uptime % over 24h.

```
hokai status
```

---

## 5. Data Model

### EndpointConfig

Persisted in `Data/endpoints.json`.

```json
{
  "id": "a1b2c3d4...",
  "url": "https://api.example.com/health",
  "interval": "00:05:00",
  "timeout": "00:00:30",
  "method": "GET",
  "expectedStatus": 200,
  "createdAt": "2026-07-10T12:00:00Z"
}
```

### CheckResult

Persisted in `Data/checks.json`. Flat list; old records are removed based on `retentionDays`.

```json
{
  "endpointId": "a1b2c3d4...",
  "timestamp": "2026-07-10T12:05:00Z",
  "isUp": true,
  "statusCode": 200,
  "responseTimeMs": 145,
  "error": null
}
```

### State (in-memory, transient)

`MonitorService` keeps the last known state of each endpoint (`UP`/`DOWN`) in memory to detect transitions and avoid duplicate notifications.

---

## 6. Internal Architecture

### 6.1 Program.cs — CLI vs Daemon Router

**Single binary, dual mode**:

```
Program.Main(args)
 ├── "run"       → Host.CreateApplicationBuilder → AddHostedService<MonitorService> → host.Run()
 ├── "endpoint"  → EndpointCommands handler → EndpointStore → console output
 ├── "status"    → EndpointStore + CheckStore → console
 └── other       → System.CommandLine shows help
```

### 6.2 MonitorService — Main Daemon

`BackgroundService` that coordinates all background execution.

```
MonitorService.ExecuteAsync()
 │
 ├── 1. Load endpoints from EndpointStore
 ├── 2. For each endpoint: spawn task with PeriodicTimer loop
 │
 ├── Reload loop (every 30s):
 │   ├── Reload endpoints.json
 │   ├── Start tasks for new endpoints
 │   └── Cancel tasks for removed endpoints
 │
 ├── Cleanup loop (every 1h):
 │   └── CheckStore.RemoveOlderThan(retentionDays)
 │
 └── Each endpoint task:
     └── await timer.WaitForNextTickAsync()
         ├── HealthCheckService.Check(endpoint)
         ├── CheckStore.Append(result)
         ├── If state transition: NotificationService.Notify(endpoint, result)
         └── Update in-memory state
```

#### CLI and Daemon Synchronization

- CLI writes to `Data/endpoints.json` and exits immediately
- Daemon re-reads the file every 30s to detect changes
- No explicit file locking (atomic write via `WriteAllText` with temp file + rename)
- No IPC, no inter-process communication

### 6.3 HealthCheckService

Responsibility: execute an HTTP health check and return a `CheckResult`.

```csharp
async Task<CheckResult> CheckAsync(EndpointConfig endpoint, CancellationToken ct)
{
    var sw = Stopwatch.StartNew();
    try
    {
        using var response = await _httpClient.SendAsync(request, ct);
        sw.Stop();
        return new CheckResult
        {
            EndpointId = endpoint.Id,
            IsUp = (int)response.StatusCode == endpoint.ExpectedStatus,
            StatusCode = (int)response.StatusCode,
            ResponseTimeMs = sw.ElapsedMilliseconds,
            Error = null
        };
    }
    catch (Exception ex)
    {
        return new CheckResult
        {
            EndpointId = endpoint.Id,
            IsUp = false,
            StatusCode = null,
            ResponseTimeMs = sw.ElapsedMilliseconds,
            Error = ex.Message
        };
    }
}
```

- Uses `IHttpClientFactory` for connection management
- Timeout is per-endpoint (not global)
- Supports any HTTP method (GET, POST, HEAD, etc.)

### 6.4 NotificationService

Responsibility: send email when an endpoint changes state.

```csharp
async Task NotifyDownAsync(EndpointConfig endpoint, CheckResult result)
async Task NotifyRecoveryAsync(EndpointConfig endpoint, CheckResult result)
```

- Uses `SmtpClient` from `System.Net.Mail`
- Reads SMTP configuration from `appsettings.json`
- Plain text templates:
  - **DOWN**: `[HOKAI ALERT] {url} is DOWN (HTTP {code}) - {error}`
  - **RECOVERY**: `[HOKAI RECOVERY] {url} is UP ({responseTime}ms)`

### 6.5 EndpointStore

Responsibility: CRUD operations on `Data/endpoints.json`.

- `IEnumerable<EndpointConfig> GetAll()`
- `EndpointConfig? GetById(string id)`
- `void Add(EndpointConfig config)`
- `void Remove(string id)`

### 6.6 CheckStore

Responsibility: append results and calculate uptime from `Data/checks.json`.

- `void Append(CheckResult result)`
- `double GetUptime(string endpointId, TimeSpan window)` — e.g. last 24h
- `CheckResult? GetLastCheck(string endpointId)`
- `void RemoveOlderThan(TimeSpan retention)`

---

## 7. Notification Flow

```
Previous state (memory)   Current Check     Action
─────────────────────────────────────────────────────────
null (first check)        UP                None
null (first check)        DOWN              None
UP                        UP                None
UP                        DOWN              Email DOWN
DOWN                      DOWN              None
DOWN                      UP                Email RECOVERY
```

Rule: notification is only sent on **state transitions**, preventing spam.

---

## 8. Configuration

`appsettings.json`:

```json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 25,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "FromAddress": "hokai@localhost",
    "ToAddresses": ["admin@example.com"]
  },
  "DataDirectory": "Data",
  "RetentionDays": 30
}
```

- `DataDirectory`: relative to the working directory or an absolute path
- `RetentionDays`: checks older than this are automatically removed
- File is optional: if it doesn't exist, reasonable defaults are used

---

## 9. Detailed Dependencies

### NuGet (external)

| Package | Version | Reason |
|---|---|---|
| `System.CommandLine` | 2.0.x | CLI parsing with subcommands, auto-help, validation |

### SDK (built-in, no NuGet)

| Namespace | Usage |
|---|---|
| `Microsoft.Extensions.Hosting` | Worker Service, DI, lifecycle |
| `Microsoft.Extensions.Http` | `IHttpClientFactory`, connection pooling |
| `Microsoft.Extensions.Configuration.Json` | Read `appsettings.json` |
| `Microsoft.Extensions.Logging.Console` | Daemon console logging |
| `System.Net.Mail` | `SmtpClient`, `MailMessage` |
| `System.Net.Http` | `HttpClient` for health checks |
| `System.Text.Json` | Data file serialization |
| `System.Threading` | `PeriodicTimer` for scheduling |

---

## 10. Cross-Platform Considerations

| Aspect | Strategy |
|---|---|
| Data directory path | `Path.Combine` + `Environment.SpecialFolder` or configurable |
| Daemonization | See [Daemonization](daemonization.md) — `systemd` (Linux), `launchd` (macOS), Windows Service |
| File newlines | `Environment.NewLine` |
| Encoding | UTF-8 without BOM (default `System.Text.Json`) |
| Single binary | `dotnet publish -r <rid> --self-contained true` or `-p:PublishSingleFile=true` |

---

## 11. Future Improvements

- [ ] TCP health check support (socket connection)
- [ ] ICMP ping support
- [ ] Embedded web dashboard (minimal API inside the daemon)
- [ ] Webhook notifications (Slack, Discord, etc.)
- [ ] Uptime history with charts (CSV export)
- [ ] Prometheus metrics via HTTP endpoint
- [ ] SMTP password encryption at rest
- [ ] Multi-tenancy support (different recipients per endpoint)
- [ ] Daemon self health-check / watchdog
- [ ] Integration tests with mock SMTP server

For installation-related improvements (Homebrew, APT, winget, Docker, auto-update) see [Installation > Future Improvements](installation.md#10-future-improvements). For daemon/service improvements (log tailing, multiple instances) see [Daemonization > Pending Decisions](daemonization.md#7-pending-decisions).

---

## 12. Glossary

| Term | Definition |
|---|---|
| **Endpoint** | HTTP/HTTPS URL being monitored |
| **Check** | Individual HTTP request |
| **Uptime %** | (successful checks / total checks) × 100 over the last 24h |
| **Daemon** | `hokai run` process that runs continuously |
| **Transition** | State change UP ↔ DOWN that triggers a notification |
| **IPC** | Inter-Process Communication (deliberately not used) |
