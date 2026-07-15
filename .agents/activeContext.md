# Active Context

**Last updated**: 2026-07-15

## Current focus
- Improving release workflow: issue-first phase tracking for accurate milestone progress
- Updating AGENTS.md, PR template, issue template, and release docs

## Recent changes (since last update)
- v0.2.0-alpha.1 fully released and published
- All 13 implementation phases, hardening patches, and documentation PRs merged and CI-green
- Process improvement: release phases now tracked via issues (not just PRs) for accurate milestone percentages

## Current state
- `dev` at `ab722bba` — CI green, 228 tests, zero warnings, six AOT jobs green
- `main` at `v0.1.0-rc.2` — awaiting release integration
- Qualification: 87% size reduction, 89% startup improvement vs rc.2
- `main` requires PR, approval, linear history, squash/rebase only
- `.docs/pt-BR/release.md` stale (JIT-era content) — being corrected in this PR
- Blockers: none

## Next steps
1. Complete AGENTS.md issue-tracking workflow update (this PR)
2. Open draft PR, monitor CI, mark ready for review
3. After merge, apply new workflow to next release cycle
