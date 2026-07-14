# Hokai — Performance

> Size, startup, memory baselines, and storage optimization strategy.

**Related docs**: [Architecture](architecture.md) | [Release](release.md)

---

## 1. Release Size Baselines

All measurements are for self-contained single-file binaries published with `PublishSingleFile=true`, `PublishSelfContained=true`, and `PublishTrimmed=false`.

### 1.1 Executable Sizes (uncompressed)

| RID | Approximate Size |
|---|---|
| linux-x64 | ~71 MiB |
| linux-arm64 | ~71 MiB |
| osx-x64 | ~76 MiB |
| osx-arm64 | ~76 MiB |
| win-x64 | ~80 MiB |
| win-arm64 | ~82 MiB |

*Measured from `v0.1.0-rc.1` single-file publish output on each platform. Rounded to nearest MiB.*

### 1.2 Archive Sizes (compressed)

| RID | Archive | Approximate Size |
|---|---|---|
| linux-x64 | `.tar.gz` | ~29 MiB |
| linux-arm64 | `.tar.gz` | ~29 MiB |
| osx-x64 | `.tar.gz` | ~31 MiB |
| osx-arm64 | `.tar.gz` | ~32 MiB |
| win-x64 | `.zip` | ~32 MiB |
| win-arm64 | `.zip` | ~33 MiB |

*Combined distribution size: ~182.7 MiB total across six archives.*

### 1.3 Docker Image Sizes

Multi-architecture image published to `ghcr.io/tiagosantini/hokai`.

| Platform | Approximate Size |
|---|---|
| linux/amd64 | ~38 MiB (compressed) |
| linux/arm64 | ~37 MiB (compressed) |

*Based on `runtime-deps:10.0-noble-chiseled` with the trimmed self-contained binary.*

---

## 2. Startup Time

Hokai CLI commands perform the following work at startup:
- Configuration path resolution (4-step fallback chain)
- JSON config loading and binding (source-generated from `v0.1.0-rc.2`)
- Store initialization (no database connection, no network)

The daemon (`hokai run`) additionally:
- OS service registration check
- HTTP client factory creation
- Endpoint config loading

**Estimated baseline for CLI commands**: < 200 ms on modern hardware.
*Precise benchmarks will be added in a future release.*

### 2.1 AOT Startup Improvement Target

NativeAOT compilation (planned for `v0.2.0-alpha.1`) targets:
- CLI command startup: < 100 ms
- Daemon startup: < 150 ms
- ≥20% improvement over current JIT-compiled startup

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

### 4.2 Batch Summary Optimization (v0.1.0-rc.2)

The `CheckStore.GetBatchSummariesAsync` method reads `checks.json` once and computes uptime and last-check summaries for all endpoints in a single pass.

| Command | Before | After |
|---|---|---|
| `endpoint list` | E reads of `checks.json` | 1 read of `checks.json` |
| `status` | 2E reads of `checks.json` | 1 read of `checks.json` |

**Result**: Status and list commands now scale O(E + C) instead of O(E × C).

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

- [ ] Automated performance benchmarks in CI
- [ ] NativeAOT publishing (≥30% size reduction, ≥20% startup improvement)
- [ ] Append-oriented storage format
- [ ] Memory-mapped check file for high-frequency monitoring
- [ ] Config hot-reload without file polling
- [ ] Parallel health checks (currently sequential per endpoint)
