# Active Context

**Last updated**: 2026-07-13

## Current focus
- Phases 1-6 complete
- Phase 7 Quality pending (scripts, Docker, CI)
- All three ServiceManager backends implemented (systemd, launchd, Windows)

## Recent changes
- Phase 6 contracts reconciled across 7 English and Portuguese docs
- `IProcessRunner` + `ProcessRunner` with safe argument handling and process-tree cancellation
- `ApplicationPaths`, `ConfigurationPathResolver`, `AppSettingsLoader` for canonical OS config
- `IServiceManager` updated: `UninstallAsync(bool purge, ...)`
- `ServiceCommands` gained `--purge` option; extracted shared `CommandTestHarness`
- `ServiceManager` facade with platform detection
- `SystemdServiceManager`: systemd unit, user/group creation, permissions, lifecycle
- `LaunchdServiceManager`: user LaunchAgent, plist generation, bootstrap/bootout/kickstart
- `WindowsServiceManager`: `sc.exe` create/config, `LocalService` account, `icacls` ACLs
- `ServiceCollectionExtensions`: three-tier DI (`AddHokaiCore`, `AddHokaiMonitoring`, `AddHokaiDaemon`)
- `HokaiApplication.RunAsync`: config resolver, host builder, CLI/daemon router
- `Program.cs` activated; `hokai --help` shows all commands
- Context-aware `UseSystemd()` and `UseWindowsService()` registered unconditionally
- Phase 6: 155 tests pass, 0 build warnings

## Next steps
- Phase 7: installer scripts, Docker support, CI workflows
- Manual validation on macOS and Windows runners

## Blockers
- None — all Phase 6 deliverables are implemented
