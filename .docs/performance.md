# Hokai — Performance

> Size, startup, memory baselines, and storage optimization strategy.

**Related docs**: [Architecture](architecture.md) | [Release](release.md)

---

## 1. Release Size Baselines

All measurements are for self-contained single-file binaries published with `PublishSingleFile=true` and `PublishSelfContained=true`.

### 1.1 AOT Binary Sizes (v0.2.0-alpha.1, linux-x64)

| Metric | rc.2 (JIT, PublishTrimmed=false) | Candidate (AOT, TrimMode=full) | Reduction |
|---|---|---|---|
| Uncompressed binary | 75,600,670 B (~72 MiB) | 9,877,600 B (~9.4 MiB) | 87% |
| Compressed `.tar.gz` | ~29 MiB | ~TBD | — |

*Measured from CI run 29388189153. Full six-RID AOT size table pending release dry-run. Docker images target ~82% smaller than rc.2 equivalents on `runtime-deps` chiseled base.*

### 1.2 RC.2 JIT Baselines (historical)

Per-RID rc.2 executable sizes (uncompressed, PublishTrimmed=false):

| RID | Approximate Size |
|---|---|
| linux-x64 | ~71 MiB |
| linux-arm64 | ~71 MiB |
| osx-x64 | ~76 MiB |
| osx-arm64 | ~76 MiB |
| win-x64 | ~80 MiB |
| win-arm64 | ~82 MiB |

*Measured from `v0.1.0-rc.1` single-file publish output on each platform. Rounded to nearest MiB.*

---

## 2. Startup Time

### 2.1 Cold Startup (CLI `--version`)

Measured on ubuntu-24.04 x64 via `scripts/bench-aot.sh` (7 cold runs, median):

| Version | Compilation | Median Startup | Source |
|---|---|---|---|
| rc.2 | JIT | 174 ms | Release download |
| Candidate | NativeAOT | 20 ms | CI publish output |

**Improvement**: 89% faster cold startup versus rc.2 JIT baseline.

Hokai CLI commands perform the following work at startup:
- Configuration path resolution (4-step fallback chain)
- JSON config loading and binding (source-generated)
- Store initialization (no database connection, no network)

The daemon (`hokai run`) additionally:
- OS service registration check
- HTTP client factory creation
- Endpoint config loading

---

## 3. Memory Usage

### 3.1 Memory Profile

| Component | Estimated Resident Memory |
|---|---|
| CLI command (short-lived) | ~15–20 MiB |
| Daemon (`hokai run`) | ~25–35 MiB (idle, per endpoint overhead minimal) |

*Daemon memory grows with check history retained in the 24h window. Memory is bounded by the retention window and the number of endpoints.*

### 3.2 AOT Memory Reduction Target

NativeAOT compilation targets ≥15% reduction in working set due to:
- No JIT code generation overhead
- No tiered compilation bookkeeping
- Static type layout at compile time

---

## 4. Storage Optimization

### 4.1 Current State

| File | Access Pattern | Complexity |
|---|---|---|
| `endpoints.json` | Read on startup, write on add/remove | O(E) |
| `checks.json` | Read per query, write on append/prune | O(C) per read |

Each endpoint status query reads the entire `checks.json` file. With E endpoints and C checks, checking all endpoints requires O(E × C) total work.

### 4.2 Batch Summary Optimization (v0.1.0-rc.2 → v0.2.0-alpha.1)

`GetBatchSummariesAsync` reads `checks.json` once and computes uptime and last-check summaries for all endpoints in a single pass. Further optimized to O(C) single-pass grouping in v0.2.0-alpha.1 (#80).

| Complexity | Before (rc.1) | After (#80) |
|---|---|---|
| `endpoint list` | E reads of `checks.json` | 1 read, 1 pass |
| `status` | 2E reads of `checks.json` | 1 read, 1 pass |
| Algorithmic | O(E × C) | O(C) |

### 4.3 Future Append-Oriented Format (planned)

Currently every append rewrites the entire `checks.json` array. Future releases will explore:
- Append-oriented JSON with periodic compaction
- Rolling log segments with time-based rotation
- Memory-mapped file access for high-frequency checks

These changes require a storage format migration and are deferred to a future major version.

---

## 5. Configuration Binding Performance

| `v0.1.0-rc.1` | `v0.1.0-rc.2` |
|---|---|
| Reflection-based `ConfigurationBuilder.Bind()` | Source-generated binding (`EnableConfigurationBindingGenerator`) |
| Runtime type inspection | Compile-time property accessors |
| Slower cold start | Zero-reflection config loading |

---

## 6. Benchmarks

*Planned for a future release. Target measurements:*

- CLI command end-to-end latency (cold start + execution)
- Daemon steady-state memory (1, 10, 50 endpoints)
- Health check throughput (checks/second on idle HTTP endpoint)
- File I/O throughput (appends/minute, reads/minute)
- Startup time (time-to-first-check)

---

## 7. Future Improvements

- [x] NativeAOT publishing (87% size reduction, 89% startup improvement, #84)
- [ ] Full six-RID size and startup qualification table
- [ ] Automated performance regression detection in CI beyond linux-x64
- [ ] Append-oriented storage format
- [ ] Memory-mapped check file for high-frequency monitoring
- [ ] Config hot-reload without file polling
- [ ] Parallel health checks (currently sequential per endpoint)

### Reproducible Benchmarks

CI-qualified via `scripts/bench-aot.sh`:
- Downloads rc.2 baseline from GitHub releases
- Measures cold startup (median of 7 timed runs after 3 warmup)
- Measures uncompressed binary size
- Enforces ≥30% size reduction and ≥20% startup improvement
