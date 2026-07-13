# Active Context

**Last updated**: 2026-07-13

## Current focus
- Phase 7 Quality implementation fixes complete
- Phase 8 Documentation refresh: all docs reconciled with implementation
- Tests: 183 passing, Release build 0 warnings
- Next: manual validation on macOS and Windows, then first release tag

## Recent changes (PRs #43-48)
- CLI: --config registered as real root option (fixes native service invocation)
- Daemon: cross-platform privileged-process detection
- Build: Docker image builds and runs with non-root user
- Scripts: portable SHA-256, mkdir -p, fixed macOS purge
- CI: action refs, version propagation, restore, win-arm64 matrix
- Docs: README rewrite, configuration reference EN+PT, docs index, architecture/daemon/release reconciliation

## Next steps
- Manual validation on macOS and Windows
- First release tag (v0.1.0)

## Blockers
- None — all critical defects fixed
