# Active Context

**Last updated**: 2026-07-13

## Current focus
- Phase 3 Stores implementation is complete
- Phase 4 Services implementation is complete
- Phase 5 CLI Commands implementation is complete
- Next: implement ServiceManager platform backends (Phase 6)

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
- Reconciliation rejects duplicate or failed reloads while preserving active workers and transient state
- Release build and 89 tests pass with 96.06% line coverage
- Cleanup uses configured retention, starts after the first hourly tick, and contains ordinary failures
- Phase 4 Release build and 94 tests pass with 96.02% line coverage
- Endpoint reload validation rejects nonpositive intervals before replacing active workers
- IServiceManager contract defines platform-agnostic install/uninstall/start/stop/status with caller cancellation propagation
- EndpointCommands support add, list, and remove subcommands with argument validation and formatted output
- StatusCommand displays per-endpoint last check, response time, and 24-hour uptime
- ServiceCommands delegate OS service lifecycle to IServiceManager with error handling and user feedback
- Phase 5 Release build and 131 tests pass with 96.89% line coverage

## Next steps
- Implement ServiceManager with platform backends (systemd, launchd, Windows)
- Implement Program.cs CLI router
- Set up CI workflows

## Blockers
- None currently — all prerequisites are in place
