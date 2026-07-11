# Progress

**Last updated**: 2026-07-10

## What works
- Repository initialized with git (first commit on main)
- `README.md` — project intro, quick start, usage, config reference, contributing
- `.docs/` — 3 design docs with Portuguese translations
  - `architecture.md` — application design, data model, services
  - `daemonization.md` — OS service integration (systemd, launchd, Windows)
  - `installation.md` — install methods, idempotency, uninstall procedures
- `AGENTS.md` — agent instructions (memory bank, design docs, version control, testing, security, dependencies)
- `.github/PULL_REQUEST_TEMPLATE.md` — PR description template
- `.agents/` — memory bank files (productContext, activeContext, systemPatterns, techContext, progress)
- `.gitignore` — .NET project ignores
- First commit: `chore: initial project scaffold`

## What's left to build

### Phase 1 — Scaffold
- [ ] Create dotnet solution (`hokai.sln`) + console project (`src/Hokai/Hokai.csproj`)
- [ ] Add NuGet package references
- [ ] Create test project (`tests/Hokai.Tests/Hokai.Tests.csproj`)
- [ ] Add appsettings.json template to project

### Phase 2 — Models
- [ ] `EndpointConfig` — endpoint URL, interval, timeout, method, expected status
- [ ] `CheckResult` — timestamp, isUp, status code, response time, error
- [ ] `SmtpSettings` / `AppSettings` — SMTP config POCO

### Phase 3 — Stores
- [ ] `EndpointStore` — CRUD on endpoints.json (thread-safe)
- [ ] `CheckStore` — append results, uptime %, pruning

### Phase 4 — Services
- [ ] `HealthCheckService` — HTTP request with timeout, response measurement
- [ ] `NotificationService` — email via SmtpClient, DOWN/RECOVERY templates
- [ ] `MonitorService` — BackgroundService with PeriodicTimer loops, state tracking

### Phase 5 — CLI
- [ ] `EndpointCommands` — add/list/remove endpoints
- [ ] `StatusCommand` — show uptime % and last check per endpoint
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
- None yet (no code has been implemented)
