# Active Context

**Last updated**: 2026-07-14

## Current focus
- Release readiness: all prerequisite PRs merged, dev ready for main integration
- Tests: 206 passing, Release build 0 warnings, CI green on all three OSes
- Next: dev → main integration PR, then tag v0.1.0-rc.1

## Recent changes (PRs #53-55)
- Release workflow: main ancestry validation, dry-run support, strict smoke tests
- Docker: version propagation, image smoke test, CI Docker build job
- Docs: placeholder URLs replaced, Docker commands fixed

## Next steps
1. Run release dry run from dev to verify all six RIDs and artifacts
2. Create dev → main integration PR
3. After merge, tag main with v0.1.0-rc.1
4. Validate draft assets, publish, verify GHCR

## Blockers
- None — all critical release defects fixed
