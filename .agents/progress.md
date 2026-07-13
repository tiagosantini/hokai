# Progress

**Last updated**: 2026-07-13

## What works
- Repository initialized with git, README, design docs, AGENTS.md, pull request template
- Memory bank: productContext, activeContext, systemPatterns, techContext, progress
- Solution scaffold: hokai.slnx, src/Hokai, tests/Hokai.Tests
- Models: EndpointConfig, CheckResult, AppSettings, SmtpSettings
- Stores: EndpointStore (CRUD, atomic JSON), CheckStore (append, uptime, retention)
- Services: HealthCheckService, NotificationService, MonitorService (state machine, scheduling, reconciliation)
- CLI: endpoint add/list/remove, status, service install/uninstall/start/stop/status
- CLI option: --config/-c registered as real root option
- Daemon: ServiceManager facade, systemd/launchd/Windows backends
- ProcessRunner: native process execution with cancellation
- ApplicationPaths, ConfigurationPathResolver, AppSettingsLoader
- PlatformContext: cross-platform privileged-process detection
- ServiceCollectionExtensions: three-tier DI
- HokaiApplication: CLI/daemon router, Program.cs
- Configuration reference: full docs EN+PT
- Build: reproducible, pinned SDK 10.0.301, locked packages, single-file
- Scripts: install.sh, uninstall.sh, install.ps1, uninstall.ps1
- Docker: multi-stage Dockerfile, compose.yml, non-root user
- CI: three-OS matrix, release workflow, GHCR publishing
- 183 tests pass, Release build 0 warnings

### Phase 1 — Scaffold
- [x] Create dotnet solution, console project, test project, appsettings.json

### Phase 2 — Models
- [x] EndpointConfig, CheckResult, SmtpSettings, AppSettings

### Phase 3 — Stores
- [x] EndpointStore (CRUD), CheckStore (append, uptime, retention)

### Phase 4 — Services
- [x] HealthCheckService, NotificationService, MonitorService (state machine, scheduling, reconciliation, retention)

### Phase 5 — CLI
- [x] EndpointCommands, StatusCommand, ServiceCommands

### Phase 6 — Daemon
- [x] ProcessRunner, ApplicationPaths, ConfigurationPathResolver, AppSettingsLoader
- [x] ServiceManager facade, systemd/launchd/Windows backends
- [x] PlatformContext, ServiceCollectionExtensions, HokaiApplication, Program.cs

### Phase 7 — Quality
- [x] --config registered as real CLI root option
- [x] Cross-platform privileged-process detection
- [x] Docker build unblocked, non-root user
- [x] Installer scripts hardened (portable SHA-256, macOS purge fixed)
- [x] CI/workflow fixes (action refs, version propagation, win-arm64)
- [x] Documentation reconciled with implementation

## Known issues
- Code coverage target (85%/75%) not yet enforced; current coverage ~63% lines / ~53% branches
- No integration tests for full application routing
- No GitHub Releases published yet (v0.1.0 pending)
