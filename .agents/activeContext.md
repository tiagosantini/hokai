# Active Context

**Last updated**: 2026-07-14

## Current focus
- Test stability: fixing cross-platform test defects that fail on Windows and macOS CI
- Tests: 205 passing, Release build 0 warnings
- Next: confirm CI passes on all three OSes, then first release tag

## Recent changes
- Locked restores fixed: `PublishSelfContained` + six `RuntimeIdentifiers`
- Tests reorganized: systemd/launchd/Windows tests target their concrete backends
- Path test uses host-native `Path.Combine` instead of hardcoded Unix separators
- MonitorService watchdog increased from 1s to 10s for CI stability
- Launchd install test added (no elevation required)

## Next steps
- Verify CI passes on Windows and macOS
- First release tag (v0.1.0)

## Blockers
- None
