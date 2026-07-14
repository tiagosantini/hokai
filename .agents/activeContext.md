# Active Context

**Last updated**: 2026-07-14

## Current focus
- Release preparation: hardening release workflow, Docker versioning, docs
- Tests: 206 passing, Release build 0 warnings, CI green on all three OSes
- Next: fix release blockers, then create v0.1.0-rc.1

## Recent changes
- Release workflow: main ancestry validation, workflow_dispatch dry runs, strict smoke tests (exit code + version check), archive content verification, checksum self-validation
- Docker: release version propagation forthcoming
- Docs: release flow aligned with actual workflow

## Next steps
- Fix Docker version propagation
- Correct public installation docs
- dev → main integration
- Tag v0.1.0-rc.1

## Blockers
- Release workflow not yet proven in CI (dry run pending)
