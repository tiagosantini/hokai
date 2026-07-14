# Progress

**Last updated**: 2026-07-14

## What works
- Repository initialized with git, README, design docs, AGENTS.md, pull request template
- Memory bank: productContext, activeContext, systemPatterns, techContext, progress
- Solution scaffold: hokai.slnx, src/Hokai, tests/Hokai.Tests
- Models: EndpointConfig, CheckResult, AppSettings, SmtpSettings
- Stores: EndpointStore (CRUD, atomic JSON), CheckStore (append, uptime, retention, batch summaries)
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
- Build: reproducible, pinned SDK 10.0.301, six-RID locked packages, single-file with PublishSelfContained
- Scripts: install.sh, uninstall.sh, install.ps1, uninstall.ps1
- Docker: multi-stage Dockerfile, compose.yml, non-root user
- CI: three-OS matrix, release workflow, GHCR publishing, Docker CI job
- 210 tests pass, Release build 0 warnings
- Release readiness: main ancestry validation, dry-run support, strict smoke tests
- Performance docs: size/startup/memory baselines, batch summary optimization
- Configuration: source-generated binding (EnableConfigurationBindingGenerator)
- JSON serialization: AOT-ready source-generated context (HokaiJsonContext)
- README: Quick Start section per platform, updated status, performance docs link
- v0.1.0-rc.2 draft release with 11 assets and full description

### Phase 1 — Scaffold
- [x] Create dotnet solution, console project, test project, appsettings.json

### Phase 2 — Models
- [x] EndpointConfig, CheckResult, SmtpSettings, AppSettings

### Phase 3 — Stores
- [x] EndpointStore (CRUD), CheckStore (append, uptime, retention, batch summaries)

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
- [x] Docker build unblocked, non-root user, version propagation
- [x] Installer scripts hardened (portable SHA-256, macOS purge fixed)
- [x] CI/workflow fixes (action refs, version propagation, win-arm64, Docker CI)
- [x] Release workflow hardened (main ancestry, dry-run, strict smoke tests)
- [x] Documentation reconciled with implementation
- [x] v0.1.0-rc.1 published with six-platform assets and GHCR image

### Phase 8 — Hardening (v0.1.0-rc.2)
- [x] Docker publication: digest syntax fix, cache optimization, CI image reuse
- [x] Release smoke test: strict version checking without `|| true`
- [x] Service manager: Windows exit-code validation, Linux token propagation, macOS async UID
- [x] Batch endpoint summaries: O(E+C) status and list commands
- [x] Source-generated configuration binding (EnableConfigurationBindingGenerator)
- [x] Source-generated JSON metadata (HokaiJsonContext)
- [x] Performance documentation (EN + PT)

## Known issues
- Code coverage target (85%/75%) not yet enforced; current coverage ~63% lines / ~53% branches
- No integration tests for full application routing
- Docker attestation step was skipped on rc.1 due to digest reference bug (fixed in rc.2)

## Separate Follow-Ups

### Fixed in rc.2
- Docker smoke test digest syntax: `@sha256:` instead of `:sha256:` — **fixed**
- Docker cache export: `mode=min` instead of `mode=max` — **fixed**
- CI docker job: load from buildx instead of rebuilding — **fixed**
- Release `--version` smoke test: removed `|| true` — **fixed**
- Windows `InstallAsync`: validates `sc.exe config/create` and `icacls.exe` exit codes — **fixed**
- Linux service manager: synchronous helpers propagate cancellation tokens — **fixed**
- macOS `GetUid`: uses cancellation token instead of `CancellationToken.None` — **fixed**
- Status/list commands: batch read O(E+C) instead of O(E×C) — **fixed**
- Config binding: source-generated instead of reflection-based — **fixed**
- JSON serialization: AOT-ready source-generated context — **fixed**

### Remaining
- macOS home directory: already uses `PlatformContext.HomeDirectory`; no change needed
- Release `workflow_dispatch`: still requires SemVer tags (deferred)
- NativeAOT: planned for v0.2.0-alpha.1
- Append-oriented storage format: planned for future major version
