# Hokai — Release Process

> How Hokai is built, packaged, and distributed.

## 1. Versioning

Hokai follows strict Semantic Versioning (MAJOR.MINOR.PATCH).

- Git tags must match `v*.*.*` (e.g. `v1.0.0`).
- The tag must point to a commit on the `main` branch.
- Version metadata is embedded at build time via `-p:Version=<tag>`.

## 2. Build Artifacts

Every release produces six self-contained single-file binaries:

| Asset | Platform |
|---|---|
| `hokai-linux-x64.tar.gz` | Linux x86_64 |
| `hokai-linux-arm64.tar.gz` | Linux ARM64 |
| `hokai-osx-x64.tar.gz` | macOS x86_64 |
| `hokai-osx-arm64.tar.gz` | macOS ARM64 |
| `hokai-win-x64.zip` | Windows x86_64 |
| `hokai-win-arm64.zip` | Windows ARM64 |

All archives contain a single executable (`hokai` on Unix, `hokai.exe` on Windows) built with `PublishSingleFile=true` and `SelfContained=true`.

Additional release assets:

| Asset | Description |
|---|---|
| `install.sh` | Unix installer (Linux + macOS) |
| `uninstall.sh` | Unix uninstaller |
| `install.ps1` | Windows installer |
| `uninstall.ps1` | Windows uninstaller |
| `SHA256SUMS` | SHA-256 checksums of all archives and scripts |

## 3. Release Flow

1. A push of a tag `vX.Y.Z` to `main` triggers the release workflow.
2. The workflow validates the tag matches strict SemVer and the tagged commit is on `main`.
3. All tests pass on Linux, macOS, and Windows.
4. All 6 RIDs are published and smoke-tested.
5. A GitHub Release draft is created with all assets and checksums.
6. The release is published manually after final review.
7. Publishing the GitHub Release triggers the GHCR image build.

## 4. Docker Images

Source: `Dockerfile` using multi-stage build.

Images are published to `ghcr.io/tiagosantini/hokai` with these tags:

| Tag | Example |
|---|---|
| Full version | `1.2.3` |
| Minor version | `1.2` |
| Major version | `1` |
| Latest stable | `latest` |

Images support `linux/amd64` and `linux/arm64`.

Pre-releases (e.g. `v1.2.3-beta.1`) receive only the full version tag and do not move `latest`.

## 5. Signature and Provenance

- Release workflow generates artifact attestations for supply chain provenance.
- All release assets include `SHA256SUMS` for integrity verification.
- Code signing (Authenticode on Windows, notarization on macOS) is planned as a future improvement.

## 6. Future Improvements

- [ ] Code signing (Authenticode for Windows, Apple notarization for macOS)
- [ ] .NET Global Tool publication on NuGet.org
- [ ] Homebrew formula (`brew install hokai`)
- [ ] APT repository (`apt install hokai`)
- [ ] winget package (`winget install hokai`)
