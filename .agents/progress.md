# Progress

**Last updated**: 2026-07-13

## What works
- Repository initialized with git (first commit on main)
- `README.md` — project intro, quick start, usage, config reference, contributing
- `.docs/` — 3 design docs with Portuguese translations
  - `architecture.md` — application design, data model, services
  - `daemonization.md` — OS service integration (systemd, launchd, Windows)
  - `installation.md` — install methods, idempotency, uninstall procedures
- `AGENTS.md` — agent instructions (memory bank, design docs, worktrees, code quality, testing, security, dependencies)
- `.github/PULL_REQUEST_TEMPLATE.md` — PR description template
- `.agents/` — memory bank files (productContext, activeContext, systemPatterns, techContext, progress)
- `.gitignore` — .NET project ignores
- First commit: `chore: initial project scaffold`
- `hokai.slnx` — simplified .NET 10 solution with application and test projects
- `src/Hokai/Hokai.csproj` — console application with approved dependencies
- `tests/Hokai.Tests/Hokai.Tests.csproj` — xUnit and coverage infrastructure
- `src/Hokai/appsettings.json` — configuration template copied to build output
- Phase 1 verification — Release build passes with 0 warnings; 4 tests pass; 90.9% line coverage
- `EndpointConfig` and `CheckResult` — required, nullable-aware POCOs matching the documented JSON contract
- Phase 2 verification — Release build passes with 0 warnings; 10 tests pass; 95.8% line coverage
- Phase 3 contracts — asynchronous Store interfaces, JSON array format, atomic publication, and time-window semantics documented
- EndpointStore — asynchronous reads and mutations with atomic JSON publication and in-process path locking
- EndpointStore verification — Release build passes with 0 warnings; 19 tests pass; 98.97% line coverage
- CheckStore queries — concurrent append, last-check lookup, and deterministic uptime windows
- CheckStore query verification — Release build passes with 0 warnings; 28 tests pass; 99.24% line coverage
- CheckStore retention — cutoff-aware pruning serialized with append operations
- Phase 3 verification — Release build passes with 0 warnings; 34 tests pass; 99.31% line coverage
- Store APIs and critical persistence blocks include comments for non-obvious contracts and invariants
- Phase 4 contracts — service interfaces, cancellation, notifications, scheduling, and reload semantics documented
- HealthCheckService — HTTP method/status mapping, per-endpoint timeout, caller cancellation, and transport failure handling
- HealthCheckService verification — Release build passes with 0 warnings; 52 tests pass; 99.49% line coverage
- SMTP mail sender — per-send client lifecycle, optional credentials, SSL, cancellation, and disposal
- SMTP sender verification — Release build passes with 0 warnings; 60 tests pass; 99.54% line coverage
- NotificationService — DOWN/recovery email formatting, recipient handling, and failure containment
- NotificationService verification — Release build passes with 0 warnings; 66 tests pass; 98.85% line coverage
- Monitor transitions — persistence-first state machine with first-result suppression and failure-safe state advancement
- Monitor transition verification — Release build passes with 0 warnings; 74 tests pass; 97.57% line coverage
- Monitor scheduling — immediate endpoint checks, periodic non-overlap, cancellation, and timer disposal
- Monitor scheduling verification — Release build passes with 0 warnings; 79 tests pass; 94.90% line coverage
- Monitor reconciliation — 30-second add/remove/change handling with invalid snapshot preservation
- Monitor reconciliation verification — Release build passes with 0 warnings; 89 tests pass; 96.06% line coverage
- Monitor retention — delayed hourly cleanup with validation, cancellation, and failure containment
- Phase 4 verification — Release build passes with 0 warnings; 94 tests pass; 96.02% line coverage
- Monitor reload validation preserves active workers when configured intervals are nonpositive
- IServiceManager contract — platform-agnostic OS service lifecycle abstraction
- EndpointCommands — add/list/remove subcommands with URL/interval/method validation and formatted output
- StatusCommand — per-endpoint last check, response time, and 24-hour uptime display

## What's left to build

### Phase 1 — Scaffold
- [x] Create dotnet solution (`hokai.slnx`) + console project (`src/Hokai/Hokai.csproj`)
- [x] Add NuGet package references
- [x] Create test project (`tests/Hokai.Tests/Hokai.Tests.csproj`)
- [x] Add appsettings.json template to project

### Phase 2 — Models
- [x] `EndpointConfig` — endpoint URL, interval, timeout, method, expected status
- [x] `CheckResult` — timestamp, isUp, status code, response time, error
- [x] `SmtpSettings` / `AppSettings` — SMTP config POCO (implemented with Phase 1 configuration tests)

### Phase 3 — Stores
- [x] `EndpointStore` — CRUD on endpoints.json (thread-safe)
- [x] `CheckStore` — append results, uptime %, pruning

### Phase 4 — Services
- [x] `HealthCheckService` — HTTP request with timeout, response measurement
- [x] `NotificationService` — email via SmtpClient, DOWN/RECOVERY templates
- [x] `MonitorService` — BackgroundService with PeriodicTimer loops, state tracking

### Phase 5 — CLI
- [x] `EndpointCommands` — add/list/remove endpoints
- [x] `StatusCommand` — show uptime % and last check per endpoint
- [ ] `ServiceCommands` — install/uninstall/start/stop/status

### Phase 6 — Daemon
- [ ] `ServiceManager` — platform abstraction (systemd, launchd, Windows)
- [ ] Program.cs — CLI router (run vs endpoint vs service vs status)
- [ ] Hosting integration (`UseSystemd()` / `UseWindowsService()`)

### Phase 7 — Quality
- [ ] Unit tests for all services
- [ ] Scripts (`install.sh`, `uninstall.sh`, `install.ps1`, `uninstall.ps1`)
- [ ] Dockerfile + docker-compose.yml
- [ ] CI workflows (release.yml, docker-publish.yml)

## Known issues
- None
