# System Patterns

## Architecture
- **Single binary, dual mode** — the same executable acts as CLI (one-off commands) and daemon (`hokai run`)
- **Worker Service** — daemon is a `BackgroundService` with `PeriodicTimer` loops per endpoint
- **File-based persistence** — endpoints and check results stored as JSON files in a data directory
- **No IPC** — CLI writes JSON files, daemon polls for changes every 30s

## Design patterns
- **DI with HostBuilder** — `Microsoft.Extensions.Hosting` for service registration, configuration, logging
- **Repository pattern** — `EndpointStore` and `CheckStore` abstract file I/O behind interfaces
- **Atomic JSON publication** — in-process path locks serialize mutations before a same-directory temporary file is renamed over the destination
- **State machine (transitions)** — `MonitorService` tracks UP/DOWN per endpoint in memory, only notifies on change
- **Platform abstraction** — `ServiceManager` facade delegates to `SystemdServiceManager`, `LaunchdServiceManager`, or `WindowsServiceManager`
- **Three-tier DI** — `AddHokaiCore` (settings, stores), `AddHokaiMonitoring` (health, SMTP, notifications), `AddHokaiDaemon` (MonitorService)
- **Dual-mode router** — CLI commands use a minimal DI container; `hokai run` starts the full host with MonitorService
- **Config hierarchy** — `--config` → `HOKAI_CONFIG_PATH` → canonical existing → executable-adjacent → canonical default
- **Environment overrides** — all `appsettings.json` values overridable via `HOKAI_*` env vars, even without a config file
- **Single-pass aggregation** — `GetBatchSummariesAsync` groups all checks by endpoint in one pass (O(C) instead of O(E×C))

## Code conventions
- .NET 10 with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`
- Primary constructors for DI services
- `System.Text.Json` with `JsonSerializerOptions { WriteIndented = true }`
- HttpClient via `IHttpClientFactory` (typed or named client)
- Caller cancellation propagates; endpoint HTTP timeouts become persisted DOWN results
- SmtpClient from `System.Net.Mail` (no MailKit dependency)
- One SmtpClient per send; ordinary notification failures are logged without retry
- TimeSpan for intervals and timeouts (parsed from CLI args)
- Async all the way — `async Task` for every I/O operation
- Store reads treat missing files as empty; malformed files fail without being overwritten
- `TimeProvider` supplies UTC time for deterministic uptime and retention boundaries

## Key service responsibilities

| Service | Role |
|---|---|
| `MonitorService` | Orchestrates pings, reloads config, triggers notifications, manages cleanup |
| `HealthCheckService` | Sends HTTP request, measures response time, returns CheckResult |
| `NotificationService` | Builds email body, sends via SmtpClient on state transitions |
| `EndpointStore` | CRUD on endpoints.json, thread-safe reads/writes |
| `CheckStore` | Appends check results, calculates uptime %, prunes old records |
| `ServiceManager` | Installs/uninstalls/starts/stops OS service per platform |

## Monitor invariants
- Endpoint workers check immediately, then use non-overlapping `PeriodicTimer` ticks
- Results are persisted before transitions are notified or in-memory state advances
- Changed endpoints restart with unknown state; invalid reloads preserve existing workers

## Data flow
```
User CLI → EndpointStore → endpoints.json
                                            MonitorService (polls every 30s)
                                                ↓
                              HealthCheckService.Check(endpoint)
                                                ↓
                              CheckStore.Append(result) → checks.json
                                                ↓
                              NotificationService (if transition)
```
