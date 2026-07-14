# Active Context

**Last updated**: 2026-07-14

## Current focus
- v0.1.0-rc.2 hardening prerelease implementation
- All rc.2 changes implemented in `feat/rc2-hardening` worktree
- Tests: 210 passing, Release build 0 warnings

## Recent changes (rc.2 hardening)
- Docker publication fix: digest reference `@sha256:`, cache `mode=min`, CI loads built image instead of rebuilding
- Release smoke test: removed `|| true` from `--version` check
- Service manager hardening: Windows exit-code validation for `sc.exe` and `icacls.exe`, Linux cancellation token propagation, macOS async UID resolution
- Batch endpoint summaries: `GetBatchSummariesAsync` reads `checks.json` once, O(E+C) for status/list
- Source-generated configuration binding (`EnableConfigurationBindingGenerator`)
- Source-generated JSON metadata (`HokaiJsonContext`)
- Models changed from `{ get; init; }` to `{ get; set; }` for source-gen compatibility
- New performance docs (EN + PT) with size/startup/memory baselines
- Architecture docs updated with batch summary and performance references

## Next steps
1. Commit changes, build, test (done)
2. Create release draft with tag v0.1.0-rc.2
3. Integrate into dev, then main
4. Publish GitHub release and Docker image

## Blockers
- None — all rc.2 changes implemented and tested
