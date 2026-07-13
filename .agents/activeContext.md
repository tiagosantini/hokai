# Active Context

**Last updated**: 2026-07-13

## Current focus
- Phase 7 Quality: core stabilization complete, installers and Docker ready
- CI/workflows: three-OS matrix, automated releases, GHCR docker publishing
- Coverage: tests grew from 155 to 171, coverage improving

## Recent changes (Phase 7)
- Docs: reconciled installation contracts, added release process (EN+PT-BR)
- CI: baseline build+test workflow, expanded to three-OS matrix with security scan
- Config: --config routing structural, missing config reported as error
- Duration: parser supports 30s/5m/2h/1d/500ms notation
- Hosting: settings loader and DI registration fully covered
- ProcessRunner: cancellation hardening, input validation, mid-process cancel tests
- Platform: PlatformContext isolates OS detection, used by all backends
- Systemd: data path fixed, permissions applied, strict exit-code validation
- Launchd: PlatformContext integration, native command validation, plist tests
- Windows: exit-code-based idempotency, absolute data paths, purge guard
- Build: reproducible with pinned SDK, locked packages, lowercase binary, MIT license
- Scripts: verified install.sh, uninstall.sh, install.ps1, uninstall.ps1
- Docker: multi-stage image, compose stack, non-root, explicit HOKAI_CONFIG_PATH
- Release: automated six-RID publish, SHA256SUMS, draft releases, attestations
- GHCR: multi-platform amd64/arm64 on tag publish

## Next steps
- Manual validation on macOS and Windows
- First release tag

## Blockers
- None remaining — all critical defects fixed

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
