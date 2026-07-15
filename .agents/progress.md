# Progress

**Last updated**: 2026-07-15

## What works

- All 13 v0.2.0-alpha.1 phases merged on `dev`, CI green
- 228 tests pass, Release build zero warnings
- Six-RID NativeAOT publish with functional smoke tests (add/list/status/remove)
- ldd verification on Linux artifacts
- AOT qualification: 87% size reduction, 89% startup improvement (CI-enforced)
- Docker target-platform NativeAOT build for AMD64/ARM64 via Buildx
- Release publish matrix uses six native runners (no cross-arch smoke skips)
- Installer scripts handle prerelease-only repos, mandatory SHA-256 checksums
- Linux config ownership corrected (`chown hokai:hokai` after root install)
- HOKAI_* environment variable overrides work without config file
- rc.2 storage fixtures verified: JIT and AOT read/write same JSON format
- Batch summaries: O(C) single-pass grouping (was O(E×C))
- SOURCE_SHA checksummed and attested in release archives
- Release workstation configured with explicit PublishAot, TrimMode=full, -warnaserror
- v0.2.0-alpha.1 published: six archives, Docker, attestation, all CI green

### Phase 1–8 — Foundation
- [x] Solution scaffold, models, stores, services, CLI, daemon
- [x] v0.1.0-rc.1 and rc.2 published with six RIDs + Docker

### Phase 9–11 — NativeAOT enablement
- [x] Source-generated JSON context wired into storage (#73)
- [x] PublishAot, trimming, locked AOT compiler assets (#73)
- [x] Six-RID native CI matrix (#75)

### Phase 12–13 — Docker and qualification
- [x] Target-platform AOT Docker (#82)
- [x] AOT size/startup qualification harness (#84)

### Hardening patches
- [x] Release workflow: smoke test, SOURCE_SHA, attestation (#77)
- [x] Config: HOKAI_* env vars, Linux chown (#78)
- [x] Installer: prerelease resolution, mandatory checksums (#79)
- [x] Storage: O(C) batch summaries (#80)
- [x] Storage: rc.2 fixture compatibility (#81)
- [x] Release runners: six-native matrix (#83)
- [x] Flaky test: `dotnet --version` → `hostname` (#76)

### Documentation
- [x] NativeAOT plan EN+PT, blocker table reconciled (#85)
- [x] Performance docs with measured AOT results (#86)
- [x] README synced with NativeAOT, Docker, features (#87)
- [x] Memory bank reconciliation (#88)
- [x] Release aggregation exception and dry-run sequence (#89)

## Known issues
- Coverage thresholds (85%/75%) not yet enforced (~63% lines)
- No integration tests for full application routing
- Docker publish workflow runs on QEMU for ARM64; native ARM64 CI validation deferred

## In progress
- [ ] Issue-first release tracking (AGENTS.md workflow improvement, this PR)
