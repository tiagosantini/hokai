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
- ServiceCommands — install/uninstall/start/stop/status with --purge and error containment
- Phase 5 verification — Release build passes with 0 warnings; 131 tests pass; 96.89% line coverage
- ProcessRunner — native process execution with safe argument handling, cancellation, and stdout/stderr capture
- ApplicationPaths — canonical OS paths per platform (Linux, macOS user LaunchAgent, Windows ProgramData)
- ConfigurationPathResolver — five-tier config resolution (--config, HOKAI_CONFIG_PATH, canonical, adjacent, default)
- AppSettingsLoader — JSON config loading with DataDirectory normalization relative to config file
- ServiceManager facade — platform detection and delegation to backends
- SystemdServiceManager — systemd unit, user/group creation, permissions, install/uninstall/start/stop/status
- LaunchdServiceManager — user LaunchAgent, plist XML generation, bootstrap/bootout/kickstart
- WindowsServiceManager — sc.exe create/config, LocalService account, icacls ACLs
- ServiceCollectionExtensions — three-tier DI (core, monitoring, daemon)
- HokaiApplication — CLI/daemon router with config bootstrap and host building
- Program.cs activated — hokai --help, endpoint, status, service, run commands all functional
- Phase 6: 155 tests pass, 0 build warnings

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
- [x] `ServiceCommands` — install/uninstall/start/stop/status

### Phase 6 — Daemon
- [x] `ServiceManager` — platform abstraction (systemd, launchd, Windows)
- [x] `ProcessRunner` — native process execution with cancellation
- [x] `ApplicationPaths` — canonical OS config/data/definition paths
- [x] `ConfigurationPathResolver` — hierarchical config discovery
- [x] `AppSettingsLoader` — JSON config with DataDirectory normalization
- [x] `ServiceCollectionExtensions` — three-tier DI registration
- [x] `HokaiApplication` — CLI/daemon router
- [x] `Program.cs` — CLI router (run vs endpoint vs service vs status)
- [x] Hosting integration (`UseSystemd()` / `UseWindowsService()`)

### Phase 7 — Quality
- [ ] Stabilize Phase 6 defects (config, routing, permissions, exit codes)
- [ ] Increase coverage to 85% lines / 75% branches
- [ ] Make build reproducible (lockfile, pinned versions, AssemblyName)
- [ ] Scripts (`install.sh`, `uninstall.sh`, `install.ps1`, `uninstall.ps1`)
- [ ] Dockerfile + compose.yml
- [ ] CI workflows (ci.yml, release.yml, docker-publish.yml)
- [ ] Release assets (six RIDs, SHA256SUMS, attestations)
- [ ] GHCR image (linux/amd64, linux/arm64)

## Known issues
- `--config` is not a real CLI option; parsed manually before command tree
- `run` only recognized as first argument (blocks `hokai --config ... run`)
- Linux generated config writes data to `/etc/hokai/Data` but unit allows `/var/lib/hokai`
- Native command failures silently swallowed in all three backends
- Windows elevation detected by username string, not WindowsPrincipal
- macOS hardcodes `/Users/<name>` and fallback UID `501`
- Linux doesn't apply group-writable permissions (g+rw, setgid)
- `ProcessRunner` cancellation of already-running processes is untested
- Code coverage ~63% lines / ~53% branches (target: 85%/75%)
