# Active Context

**Last updated**: 2026-07-14

## Current focus
- v0.1.0-rc.2 release draft published, documentation updated
- README updated with Quick Start section, performance docs link, and latest version references
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
- README: added Quick Start section per platform (like release description), updated status and version references, added performance docs link
- `.docs/README.md`: added performance docs entry

## Next steps
1. Review and publish the v0.1.0-rc.2 release draft
2. Docker publish workflow will trigger on release publish
3. Plan v0.2.0-alpha.1 (NativeAOT)

## Blockers
- None
