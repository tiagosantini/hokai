# Active Context

**Last updated**: 2026-07-13

## Current focus
- Feature: CLI URI truncation for endpoint list and status commands
- Tests: 202 passing, Release build 0 warnings
- Next: manual validation on macOS and Windows, then first release tag

## Recent changes
- Added UriDisplayFormatter: truncates long URIs to 50 chars preserving scheme, host suffix, and path
- Query and fragment are dropped first when space is needed
- Applied to both `endpoint list` and `status` commands
- 19 new tests (16 formatter + 3 command regression)

## Next steps
- Manual validation on macOS and Windows
- First release tag (v0.1.0)

## Blockers
- None
