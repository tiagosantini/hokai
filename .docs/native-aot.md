# Hokai — NativeAOT

> Plan and compatibility baseline for NativeAOT compilation, targeting `v0.2.0-alpha.1`.

**Related docs**: [Architecture](architecture.md) | [Performance](performance.md) | [Release](release.md)

---

## 1. Scope

NativeAOT replaces JIT-compiled self-contained single-file binaries with ahead-of-time compiled native executables. This provides smaller artifacts, faster startup, and lower memory usage.

**Target release**: `v0.2.0-alpha.1` (pre-release). Stable releases will inherit AOT after validation.

---

## 2. Toolchain Requirements

| RID | Host OS | Native Prerequisites |
|---|---|---|
| linux-x64 | Linux x64 | `clang`, `zlib1g-dev` |
| linux-arm64 | Linux ARM64 | `clang`, `zlib1g-dev` |
| osx-x64 | macOS with Xcode | Command Line Tools |
| osx-arm64 | macOS with Xcode | Command Line Tools |
| win-x64 | Windows | Visual Studio C++ Desktop workload |
| win-arm64 | Windows | ARM64 C++ build tools + Windows SDK |

CI runners provide appropriate toolchains for same-architecture builds. Cross-architecture builds (e.g. macOS x64 on ARM64 runner) require explicit toolchain configuration.

---

## 3. Linux Compatibility Baseline

NativeAOT executables link against the platform's C runtime. The release binary must support the oldest targeted glibc version.

**Baseline**: Ubuntu 24.04 (`glibc 2.39`). Build on `ubuntu-24.04` or `ubuntu-latest` runners.

**Verification**: `ldd hokai` must report no unresolved symbols on the baseline distribution.

---

## 4. AOT Readiness Status

### Current blockers (must resolve before enabling AOT)

| Blocker | Status |
|---|---|
| `HokaiJsonContext` generated but unused; persistence uses reflection-based `JsonSerializer` | Requires Phase 9 |
| `PublishTrimmed=false`, `PublishAot` not configured | Requires Phase 10 |
| Lock file lacks NativeAOT compiler assets | Requires Phase 10 |
| Docker uses forced AMD64 SDK stage; ARM64 AOT needs native toolchain | Requires Phase 12 |
| No AOT warning enforcement in CI | Requires Phase 10 |

### Likely compatible (requires AOT verification)

- `System.CommandLine` (officially trim/AOT-compatible)
- Source-generated configuration binding
- `IHttpClientFactory` / `HttpClient`
- `System.Net.Mail` / `SmtpClient`
- Host builder and DI container
- `ProcessRunner` (native process execution)
- `PeriodicTimer`
- systemd and Windows Service hosting extensions

### Deferred optimization

| Optimization | When |
|---|---|
| Narrow host builder (`CreateApplicationBuilder`) | After correctness validation |
| Conditional platform package references per RID | After six-RID AOT publishing |
| `IlcOptimizationPreference=Size` | After size baselines |
| Invariant globalization | After URI/formatting/SMTP-address tests |
| Debugger support, stack trace trimming | After error-handling review |

---

## 5. Implementation Phases

See the release milestone for phase details. This document tracks the AOT-specific phases:

| Phase | Branch | Scope |
|---|---|---|
| 9 | `refactor/storage-aot-json` | Wire `JsonTypeInfo` into `AtomicJsonFile` |
| 10 | `build/native-aot-linux` | Enable strict AOT/trimming, regenerate lock graph, Linux x64 CI |
| 11 | `build/native-aot-platforms` | Six-RID native toolchains and AOT publishing |
| 12 | `build/native-aot-docker` | Target-native AOT Docker for AMD64/ARM64 |
| 13 | `docs/aot-qualification` | Measured size/startup/memory, EN+PT doc updates |

Preceding phases (1–8) fix bugs and harden the project before AOT is enabled.

---

## 6. AOT Acceptance Criteria

Before tagging `v0.2.0-alpha.1`:

- All six release RIDs publish with `PublishAot=true` and zero warnings.
- Same-platform artifacts pass functional smoke tests.
- Existing `endpoints.json` and `checks.json` remain readable.
- JIT and AOT builds use identical storage semantics.
- Size reduction ≥30%, startup improvement ≥20% (vs rc.2 JIT baselines).
- Docker AMD64 and ARM64 images built from AOT binaries.
- No regression in existing 210+ tests.

---

## 7. Verification Commands

### Locked AOT restore

```bash
dotnet restore src/Hokai/Hokai.csproj \
  -r linux-x64 --locked-mode \
  -p:PublishAot=true -p:PublishTrimmed=true
```

### Warning-free AOT publish

```bash
dotnet publish src/Hokai/Hokai.csproj \
  -c Release -r linux-x64 --self-contained true --no-restore \
  -p:PublishAot=true -p:PublishTrimmed=true \
  -p:TrimMode=full -p:TrimmerSingleWarn=false \
  -warnaserror -o artifacts/aot/linux-x64
```

### Functional smoke test

```bash
bin=artifacts/aot/linux-x64/hokai
$bin --help
$bin --version
$bin endpoint add http://127.0.0.1:8080 --interval 1s --timeout 1s
$bin endpoint list
$bin status
$bin endpoint remove <id>
```

---

## 8. Future Improvements

- [ ] Automated AOT size regression detection in CI
- [ ] Per-RID feature switches to exclude unused platform packages
- [ ] `IlcOptimizationPreference=Size` benchmark
- [ ] Invariant globalization evaluation
- [ ] macOS Universal Binary (fat binary) investigation
