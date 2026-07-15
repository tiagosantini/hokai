# Active Context

**Last updated**: 2026-07-15

## Current focus
- v0.2.0-alpha.1 NativeAOT release qualification is complete on `dev`
- All 13 implementation phases, hardening patches, and documentation PRs merged and CI-green
- Preparing final release dry-run and `dev` → `main` integration via aggregation PR
- Fixing stale `.docs/pt-BR/release.md` and outdated fast-forward instructions

## Recent changes (since last update)
- Memory bank reconciliation (#88)
- Release aggregation exception and dry-run sequence documented (#89)
- All 25 milestone issues closed; zero open PRs targeting dev or main

## Current state
- `dev` at `ab722bba` — CI green, 228 tests, zero warnings, six AOT jobs green
- `main` at `v0.1.0-rc.2` — awaiting release integration
- Qualification: 87% size reduction, 89% startup improvement vs rc.2
- `main` requires PR, approval, linear history, squash/rebase only
- `.docs/pt-BR/release.md` stale (JIT-era content) — being corrected in this PR
- Blockers: none

## Next steps
1. Correct `.docs/release.md` §6 with PR-based flow; sync PT-BR
2. Open draft correction PR → merge into dev
3. Resolve new dev SHA; dispatch release dry-run, validate artifacts
4. Open `dev → main` aggregation PR with dry-run evidence
5. After approval and squash-merge, tag `v0.2.0-alpha.1` on main
6. Monitor release workflow, verify draft, publish
7. Verify GHCR image, installer smoke test, close milestone
