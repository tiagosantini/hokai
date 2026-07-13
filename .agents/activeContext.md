# Active Context

**Last updated**: 2026-07-13

## Current focus
- Phase 7 Quality: stabilizing Phase 6 defects before distribution
- Documentation contracts settled; release process defined
- Next: CI baseline, then config/routing/duration fixes

## Known issues (Phase 6 blockers)
- `--config` not registered as global CLI option; parsed manually
- `run` only detected as first argument (blocks `hokai --config ... run`)
- Linux default config writes data to wrong path (/etc/hokai/Data vs /var/lib/hokai)
- Native command failures silently swallowed; exit codes not validated
- Windows elevation detection uses username string comparison
- macOS hardcodes `/Users/<name>` and UID `501`
- Two DI containers built; one undisposed
- Linux permissions (g+rw, setgid) documented but not applied
- ProcessRunner cancellation of running processes untested
- Coverage ~63% lines / ~53% branches (target 85%/75%)

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
- Phase 6 has functional defects that must be resolved before release (see known issues)
