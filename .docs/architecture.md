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
| HTTP Client | `System.Net.Http` + `IHttpClientFactory` | SDK + NuGet (Microsoft) |
| SMTP | `System.Net.Mail` (SmtpClient) | SDK |
| Serialization | `System.Text.Json` | SDK |
| Timer | `System.Threading.PeriodicTimer` | SDK |
| Config | `Microsoft.Extensions.Configuration` | SDK |
| Logging | `Microsoft.Extensions.Logging` | SDK |

**Total: 2 external dependencies for the core application.** For OS service integration, 2 additional Microsoft packages are required — see [Daemonization > Dependencies](daemonization.md#1-design-decisions-settled).

---

## 3. Project Structure

```
hokai/
├── hokai.slnx
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
[
  {
    "id": "a1b2c3d4...",
    "url": "https://api.example.com/health",
    "interval": "00:05:00",
    "timeout": "00:00:30",
    "method": "GET",
    "expectedStatus": 200,
    "createdAt": "2026-07-10T12:00:00Z"
  }
]
```

### CheckResult

Persisted in `Data/checks.json`. Flat list; old records are removed based on `retentionDays`.

```json
[
  {
    "endpointId": "a1b2c3d4...",
    "timestamp": "2026-07-10T12:05:00Z",
    "isUp": true,
    "statusCode": 200,
    "responseTimeMs": 145,
    "error": null
  }
]
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
 ├── 2. For each endpoint: spawn task, check immediately, then use PeriodicTimer
 │
 ├── Reload loop (every 30s):
 │   ├── Reload endpoints.json
 │   ├── Start tasks for new endpoints
 │   ├── Cancel tasks for removed endpoints
 │   └── Restart tasks whose monitoring settings changed
 │
 ├── Cleanup loop (every 1h):
 │   └── CheckStore.RemoveOlderThan(retentionDays)
 │
 └── Each endpoint task:
     └── HealthCheckService.Check(endpoint)
         ├── CheckStore.Append(result)
         ├── If state transition: NotificationService.Notify(endpoint, result)
         ├── Update in-memory state
         └── await timer.WaitForNextTickAsync()
```

#### CLI and Daemon Synchronization

- CLI writes to `Data/endpoints.json` and exits immediately
- Daemon re-reads the file every 30s to detect changes
- No cross-process file locking; the normal workflow has one writer process per file
- Writers are serialized in-process and publish a same-directory temporary file via atomic rename
- No IPC, no inter-process communication

### 6.3 HealthCheckService

Responsibility: execute an HTTP health check and return a `CheckResult`.

```csharp
Task<CheckResult> CheckAsync(EndpointConfig endpoint, CancellationToken cancellationToken)
```

- Uses `IHttpClientFactory` for connection management
- Uses a linked token for the per-endpoint timeout; caller cancellation is rethrown
- Endpoint timeout and transport errors return DOWN with a null status code
- Timestamp is the UTC completion time and duration uses the monotonic `TimeProvider` clock
- Redirects are not followed, response bodies are not read, and methods without configured bodies send an empty request
- Only absolute HTTP/HTTPS URLs, positive timeouts, valid methods, and status codes from 100 through 599 are accepted

### 6.4 NotificationService

Responsibility: send email when an endpoint changes state.

```csharp
Task NotifyDownAsync(EndpointConfig endpoint, CheckResult result, CancellationToken cancellationToken)
Task NotifyRecoveryAsync(EndpointConfig endpoint, CheckResult result, CancellationToken cancellationToken)
```

- Uses a new `SmtpClient` from `System.Net.Mail` for each send
- Reads SMTP configuration from `appsettings.json`
- DOWN subject: `[HOKAI ALERT] {url} is DOWN`
- Recovery subject: `[HOKAI RECOVERY] {url} is UP`
- Plain-text bodies include endpoint, timestamp, expected/actual status, response time, and transport error
- Empty recipient lists skip sending. Ordinary SMTP/configuration failures are logged without retry; caller cancellation propagates

#### Monitor failure and reload policy

- Each worker owns an `EndpointMonitorSession`; `IPeriodicTimerFactory` isolates timer creation for deterministic tests.
- A result is appended before notification or state advancement. Append failure leaves state unchanged.
- Notification failure is logged and state advances, preventing repeated transition alerts.
- The first persisted result establishes state without notification.
- Removing or changing an endpoint cancels its worker and clears transient state.
- Malformed reloads and duplicate IDs are rejected while existing workers continue unchanged.
- Reloads with nonpositive endpoint intervals are rejected before any worker is replaced.
- Cleanup failures are logged and retried on the next hourly tick.

### 6.5 EndpointStore

Responsibility: CRUD operations on `Data/endpoints.json`.

- `Task<IReadOnlyList<EndpointConfig>> GetAllAsync(CancellationToken cancellationToken)`
- `Task<EndpointConfig?> GetByIdAsync(string id, CancellationToken cancellationToken)`
- `Task AddAsync(EndpointConfig config, CancellationToken cancellationToken)`
- `Task<bool> RemoveAsync(string id, CancellationToken cancellationToken)`

### 6.6 CheckStore

Responsibility: append results and calculate uptime from `Data/checks.json`.

- `Task AppendAsync(CheckResult result, CancellationToken cancellationToken)`
- `Task<double> GetUptimeAsync(string endpointId, TimeSpan window, CancellationToken cancellationToken)` — e.g. last 24h
- `Task<CheckResult?> GetLastCheckAsync(string endpointId, CancellationToken cancellationToken)`
- `Task RemoveOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken)`

### 6.7 Storage Contracts

- `IEndpointStore` and `ICheckStore` isolate file I/O from commands and services.
- Missing files are empty collections. Mutations create the data directory when needed.
- Empty, `null`, or malformed JSON is an error and is never replaced silently.
- Files are camel-case, indented JSON arrays encoded as UTF-8 without BOM.
- Mutations use a unique temporary file in the destination directory, then atomically publish it.
- A process-wide lock keyed by canonical file path serializes mutations from all store instances.
- Cross-process write conflicts are outside the initial contract; CLI writes endpoints while the daemon writes checks.
- Appending and pruning rewrite the complete JSON array; this favors initial correctness and atomic visibility over large-history scalability.
- Endpoint IDs use ordinal, case-sensitive comparison. Adding a duplicate ID fails; removing an unknown ID returns `false` without rewriting the file.
- Removing an endpoint does not remove its historical checks, which remain until retention cleanup.
- Uptime uses checks in the inclusive UTC interval `[now - window, now]`; future checks are excluded and an empty window returns `0.0`.
- Uptime windows must be positive. Retention must be non-negative and removes checks strictly older than its cutoff.
- `GetLastCheckAsync` returns the matching result with the greatest timestamp, regardless of file order.

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
| `Microsoft.Extensions.Http` | 10.0.x | `IHttpClientFactory`, handler lifecycle, connection pooling |

### SDK (built-in, no NuGet)

| Namespace | Usage |
|---|---|
| `Microsoft.Extensions.Hosting` | Worker Service, DI, lifecycle |
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
