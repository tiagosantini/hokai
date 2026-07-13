# Active Context

**Last updated**: 2026-07-12

## Current focus
- Phase 3 Stores implementation is complete
- EndpointStore is implemented with asynchronous atomic JSON persistence
- CheckStore append, last-check, and uptime queries are implemented
- CheckStore retention cleanup is implemented with append-cleanup serialization
- Phase 4 service contracts are defined
- HealthCheckService is implemented with per-endpoint timeout and cancellation separation
- SMTP mail delivery uses a configured, short-lived client per send
- NotificationService builds plain-text transition emails and contains ordinary delivery failures
- EndpointMonitorSession implements persistence-first UP/DOWN transition handling
- MonitorService schedules immediate, non-overlapping endpoint workers with graceful shutdown
- Next implementation target is endpoint reload reconciliation

## Recent changes
- Repository initialized with git
- `README.md` created with quick-start, usage examples, configuration reference
- `.docs/` created with 3 design docs (architecture, daemonization, installation)
- `.docs/pt-BR/` created with Portuguese translations
- `AGENTS.md` created with 7 sections (memory bank, design docs, version control, code quality, testing, security, dependencies)
- `.github/PULL_REQUEST_TEMPLATE.md` created
- `.agents/` memory bank created
- `.gitignore` created for .NET projects
- First commit: `chore: initial project scaffold`
- `hokai.slnx` created with application and test projects
- Approved runtime dependencies added to `src/Hokai/Hokai.csproj`
- xUnit test infrastructure created with 4 scaffold tests
- `appsettings.json`, `AppSettings`, and `SmtpSettings` created
- Release build and tests pass with 90.9% line coverage
- `EndpointConfig` and `CheckResult` implemented with documented JSON contracts
- Model test suite covers successful checks, transport failures, and serialization formats
- Release build and 10 tests pass with 95.8% line coverage
- `AGENTS.md` requires dedicated git worktrees, semantic conflict resolution, and professional code comments
- Storage contracts define JSON arrays, atomic publication, in-process writer serialization, and deterministic time boundaries
- EndpointStore supports reads, lookup, duplicate-safe addition, idempotent removal, and concurrent in-process mutations
- Release build and 19 tests pass with 98.97% line coverage
- CheckStore persists concurrent results and provides deterministic last-check and uptime queries through TimeProvider
- Release build and 28 tests pass with 99.24% line coverage
- CheckStore retention preserves cutoff records and prevents lost appends during cleanup
- Release build and 34 tests pass with 99.31% line coverage
- Storage code comments document concurrency invariants, atomic commit boundaries, and time-window semantics
- Phase 4 contracts define HTTP cancellation, email failure, monitor transition, and reload behavior
- HealthCheckService maps HTTP status, timeout, and transport outcomes with deterministic timestamps and durations
- Release build and 52 tests pass with 99.49% line coverage
- SMTP transport configuration, cancellation, ownership, and disposal are covered without external infrastructure
- Release build and 60 tests pass with 99.54% line coverage
- Notification formatting, addressing, disabled recipients, failure logging, and cancellation are covered
- Release build and 66 tests pass with 98.85% line coverage
- Monitor transition tests cover initial state, DOWN/recovery changes, ordering, failures, and cancellation
- Release build and 74 tests pass with 97.57% line coverage
- Timer abstraction and hosted-service tests avoid real-time scheduling delays
- Release build and 79 tests pass with 94.90% line coverage

## Next steps
- Implement Services layer (HealthCheckService, NotificationService, MonitorService)
- Implement CLI Commands (EndpointCommands, ServiceCommands)
- Implement ServiceManager with platform backends
- Set up CI workflows

## Blockers
- None currently — all prerequisites are in place
