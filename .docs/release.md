# Hokai — Release Process

> How Hokai is built, packaged, and distributed.

## 1. Versioning

Hokai follows strict Semantic Versioning (MAJOR.MINOR.PATCH).

- Git tags must match `v*.*.*` (e.g. `v1.0.0`, `v0.2.0-alpha.1`).
- The tag must point to a commit on the `main` branch.
- Version metadata is embedded at build time via `-p:Version=<tag>`.
- Pre-release tags (alpha, beta, rc) receive only the full version Docker tag and do not move `latest`.

## 2. Build Artifacts

Every release produces six self-contained NativeAOT binaries:

| Asset | Platform | Build |
|---|---|---|
| `hokai-linux-x64.tar.gz` | Linux x86_64 | ubuntu-24.04 |
| `hokai-linux-arm64.tar.gz` | Linux ARM64 | ubuntu-24.04-arm |
| `hokai-osx-x64.tar.gz` | macOS x86_64 | macos-15-intel |
| `hokai-osx-arm64.tar.gz` | macOS ARM64 | macos-15 |
| `hokai-win-x64.zip` | Windows x86_64 | windows-2025 |
| `hokai-win-arm64.zip` | Windows ARM64 | windows-11-arm |

All archives contain a single NativeAOT executable (`hokai` on Unix, `hokai.exe` on Windows) built with `PublishAot=true`, `PublishTrimmed=true`, `TrimMode=full`, and `-warnaserror`. Each platform builds and smoke-tests on its target-native runner — no cross-compilation or QEMU emulation in the release pipeline.

Additional release assets:

| Asset | Description |
|---|---|
| `install.sh` | Unix installer (Linux + macOS) |
| `uninstall.sh` | Unix uninstaller |
| `install.ps1` | Windows installer |
| `uninstall.ps1` | Windows uninstaller |
| `SHA256SUMS` | SHA-256 checksums of all archives and scripts |
| `SOURCE_SHA` | Git commit SHA the release was built from |

## 3. Release Flow

1. A push of an annotated tag `vX.Y.Z` to `main` triggers the release workflow.
2. The workflow validates the tag is reachable from `origin/main` and has the expected format.
3. All six RIDs are published on their target-native runners with `PublishAot=true` and `-warnaserror`.
4. Each artifact is smoke-tested on its native runner: `--help`, `--version`, endpoint add/list/status/remove, and `ldd` (Linux only).
5. `SOURCE_SHA` is recorded, then `SHA256SUMS` is generated (including `SOURCE_SHA`) and self-validated.
6. A GitHub Release draft is created with all assets and build provenance attestation.
7. The release is published manually after final review.
8. Publishing the GitHub Release triggers the multi-platform GHCR image build.

## 4. Docker Images

Source: `Dockerfile` using multi-stage build with `--platform=$BUILDPLATFORM`.

Images are published to `ghcr.io/tiagosantini/hokai` with these tags:

| Release type | Tags |
|---|---|
| Stable (e.g. `1.2.3`) | `1.2.3`, `1.2`, `1`, `latest` |
| Pre-release (e.g. `0.2.0-alpha.1`) | `0.2.0-alpha.1` only |

Images support `linux/amd64` and `linux/arm64`, built via Buildx with QEMU emulation for the SDK stage and native linking for the target architecture. The runtime stage uses `runtime-deps:10.0-noble-chiseled` with a non-root user (UID 1000).

## 5. Signature and Provenance

- `SHA256SUMS` includes all archives, scripts, and `SOURCE_SHA` for complete artifact integrity.
- `SOURCE_SHA` records the exact git commit the release was built from.
- GitHub Actions build provenance attestation covers all release assets and `SOURCE_SHA`.
- Code signing (Authenticode on Windows, notarization on macOS) is planned as a future improvement.

## 6. Multi-Phase Release Integration

The `dev → main` integration step aggregates many prior PRs. It is documented as a justified exception to the 400-line per-PR limit (see AGENTS.md exceptions: scaffold/initial/bulk).

Both `dev` and `main` are locked behind rulesets that require pull requests, linear history, and `CI / required`. Direct push and fast-forward are not possible. The release flow must use a draft `dev → main` aggregation PR that is approved and squash-merged by a reviewer.

### Precondition checklist

- [ ] `gh run list --branch dev --workflow ci.yml --limit 1` — dev CI green
- [ ] `gh pr list --state open --base dev` — no open PRs targeting dev
- [ ] `gh pr list --state open --base main` — no open PRs targeting main
- [ ] `gh release list` — target tag does not already exist
- [ ] All milestone issues closed; `.docs/` EN and PT-BR are consistent
- [ ] `dotnet build hokai.slnx -c Release -warnaserror && dotnet test hokai.slnx -c Release --no-build` — local build and tests pass at the exact SHA

### Dry-run (before integration)

Dispatch the release workflow manually against the exact `dev` SHA with `dry_run: true`. This builds, smoke-tests, packages, and checksums all six platforms without creating a draft release or pushing a tag.

```bash
gh workflow run release.yml --ref dev -f version=0.2.0-alpha.1 -f dry_run=true
gh run watch <run_id> --exit-status
gh run download <run_id> -D dist
```

Validate the dry-run output:

```bash
# Verify six archives + four scripts + SHA256SUMS + SOURCE_SHA
ls dist/hokai-{linux-x64,linux-arm64,osx-x64,osx-arm64,win-x64,win-arm64}.{tar.gz,zip}
ls dist/{install,uninstall}.{sh,ps1} dist/SHA256SUMS dist/SOURCE_SHA

# Self-validate checksums
cd dist && sha256sum -c SHA256SUMS

# Confirm SOURCE_SHA matches the dev commit
cat dist/SOURCE_SHA

# Verify archive contents (one executable each, correct version)
tar -xzf dist/hokai-linux-x64.tar.gz -O ./hokai --version
```

### Integration (after successful dry-run)

1. Create a draft `dev → main` aggregation PR using the PR template.
2. Document the LOC exception, dry-run results, and scope in the PR description.
3. Attach the milestone and required labels.
4. CI must be green; mark the PR ready for review.
5. A reviewer approves and squash-merges the PR.

### Post-merge (after PR merge)

```bash
git fetch origin
git checkout main && git pull --ff-only origin main

# Verify main matches dev after squash
git log main...dev --oneline

# Create annotated tag on the merged main commit
git tag -a v0.2.0-alpha.1 -m "v0.2.0-alpha.1: NativeAOT preview"

# Push only the tag (main was already updated by the squash merge)
git push origin v0.2.0-alpha.1
```

### Post-push verification

1. The release workflow triggers on the tag push.
2. All six platforms build, smoke-test, package, and checksum.
3. A draft release is created with assets + provenance attestation.
4. Review the draft: verify `SHA256SUMS` self-validation, `SOURCE_SHA`, artifact list.
5. Publish the draft — triggers Docker image build.
6. Verify `ghcr.io/tiagosantini/hokai:0.2.0-alpha.1` is available.
7. Run `scripts/install.sh --version v0.2.0-alpha.1` as a final smoke test.

## 7. Future Improvements

- [ ] Code signing (Authenticode for Windows, Apple notarization for macOS)
- [ ] .NET Global Tool publication on NuGet.org
- [ ] Homebrew formula (`brew install hokai`)
- [ ] APT repository (`apt install hokai`)
- [ ] winget package (`winget install hokai`)
