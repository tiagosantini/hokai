# Active Context

**Last updated**: 2026-07-14

## Current focus
- Build fix: cross-platform locked restores (NU1004 on Windows and macOS)
- Tests: 205 passing, Release build 0 warnings
- Next: verify CI passes on all three OSes, then first release tag

## Recent changes
- Replaced global `SelfContained=true` with `PublishSelfContained=true`
- Declared six `RuntimeIdentifiers` (linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64, win-arm64)
- Regenerated `packages.lock.json` with all six RID dependency graphs
- 3 new scaffold tests verify project and lock file properties

## Next steps
- CI validation on Windows and macOS
- First release tag (v0.1.0)

## Blockers
- None
