#!/usr/bin/env bash
set -euo pipefail
# bench-aot.sh — measure AOT binary size and startup vs rc.2 baseline
REPO="tiagosantini/hokai"
BASELINE_TAG="v0.1.0-rc.2"
WARMUP_RUNS=3
TIMED_RUNS=7
MIN_SIZE_REDUCTION_PCT=30
MIN_STARTUP_IMPROVEMENT_PCT=20

die() { echo "$1" >&2; exit 1; }

# --- args ---
CANDIDATE_EXE="${1:-}"
if [[ -z "$CANDIDATE_EXE" ]]; then die "usage: $0 <candidate-exe-path>"; fi
CANDIDATE_EXE="$(realpath "$CANDIDATE_EXE")"
test -f "$CANDIDATE_EXE" || die "candidate not found: $CANDIDATE_EXE"
chmod +x "$CANDIDATE_EXE" 2>/dev/null || true

# --- download baseline ---
echo "Fetching rc.2 baseline ($BASELINE_TAG)..."
TMPDIR=$(mktemp -d)
trap "rm -rf $TMPDIR" EXIT

HOST_ARCH=$(uname -m)
ARCH=x64
[[ "$HOST_ARCH" == "aarch64" || "$HOST_ARCH" == "arm64" ]] && ARCH=arm64

BASENAME="hokai-linux-${ARCH}"
DOWNLOAD_URL="https://github.com/${REPO}/releases/download/${BASELINE_TAG}/${BASENAME}.tar.gz"

curl -fsSLo "$TMPDIR/baseline.tar.gz" "$DOWNLOAD_URL" || die "failed to download baseline"
tar -xzf "$TMPDIR/baseline.tar.gz" -C "$TMPDIR/" || die "failed to extract baseline"

BASELINE_EXE=""
for f in "$TMPDIR"/*/hokai "$TMPDIR"/hokai "$TMPDIR/${BASENAME}/hokai"; do
  test -f "$f" && { BASELINE_EXE="$f"; break; }
done
test -n "$BASELINE_EXE" || die "baseline hokai binary not found in archive"
chmod +x "$BASELINE_EXE" 2>/dev/null || true

# --- size ---
CANDIDATE_SIZE=$(stat --printf=%s "$CANDIDATE_EXE" 2>/dev/null || stat -f%z "$CANDIDATE_EXE")
BASELINE_SIZE=$(stat --printf=%s "$BASELINE_EXE" 2>/dev/null || stat -f%z "$BASELINE_EXE")

echo ""
echo "=== Size ==="
printf "baseline:  %10d bytes  (rc.2)\n" "$BASELINE_SIZE"
printf "candidate: %10d bytes  (current)\n" "$CANDIDATE_SIZE"

if command -v bc >/dev/null 2>&1; then
  SIZE_REDUCTION=$(echo "scale=2; (1 - $CANDIDATE_SIZE / $BASELINE_SIZE) * 100" | bc -l)
else
  SIZE_REDUCTION=$(awk "BEGIN { printf \"%.2f\", (1 - $CANDIDATE_SIZE / $BASELINE_SIZE) * 100 }")
fi
printf "reduction:  %s%%\n" "$SIZE_REDUCTION"

if command -v bc >/dev/null 2>&1; then
  SIZE_OK=$(echo "$SIZE_REDUCTION >= $MIN_SIZE_REDUCTION_PCT" | bc -l)
else
  SIZE_OK=$(awk "BEGIN { if ($SIZE_REDUCTION >= $MIN_SIZE_REDUCTION_PCT) print 1; else print 0 }")
fi
if [[ "$SIZE_OK" -eq 0 ]]; then
  echo "FAIL: size reduction ${SIZE_REDUCTION}% < ${MIN_SIZE_REDUCTION_PCT}%" >&2
  exit 1
fi
echo "PASS: >= ${MIN_SIZE_REDUCTION_PCT}%"

# --- startup ---
echo ""
echo "=== Startup (cold, median of $TIMED_RUNS runs after $WARMUP_RUNS warmup) ==="

measure_startup() {
  local exe="$1"
  local label="$2"
  local times=()

  for i in $(seq 1 $WARMUP_RUNS); do
    "$exe" --version > /dev/null 2>&1 || true
  done

  for i in $(seq 1 $TIMED_RUNS); do
    local start
    start=$(date +%s%N)
    "$exe" --version > /dev/null 2>&1
    local end
    end=$(date +%s%N)
    local elapsed=$(( (end - start) / 1000000 ))
    times+=("$elapsed")
  done

  printf "%s\n" "${times[@]}" | sort -n | awk -v label="$label" '
    { a[NR]=$1 }
    END {
      median=a[int((NR+1)/2)]
      printf "%s:  %d ms\n", label, median
    }'
}

BASELINE_MS=$(measure_startup "$BASELINE_EXE" "baseline" | awk '{print $NF}' | tr -d 'ms')
CANDIDATE_MS=$(measure_startup "$CANDIDATE_EXE" "candidate" | awk '{print $NF}' | tr -d 'ms')

if command -v bc >/dev/null 2>&1; then
  STARTUP_IMPROVE=$(echo "scale=2; (1 - $CANDIDATE_MS / $BASELINE_MS) * 100" | bc -l)
else
  STARTUP_IMPROVE=$(awk "BEGIN { printf \"%.2f\", (1 - $CANDIDATE_MS / $BASELINE_MS) * 100 }")
fi
printf "improvement: %s%%\n" "$STARTUP_IMPROVE"

if command -v bc >/dev/null 2>&1; then
  STARTUP_OK=$(echo "$STARTUP_IMPROVE >= $MIN_STARTUP_IMPROVEMENT_PCT" | bc -l)
else
  STARTUP_OK=$(awk "BEGIN { if ($STARTUP_IMPROVE >= $MIN_STARTUP_IMPROVEMENT_PCT) print 1; else print 0 }")
fi
if [[ "$STARTUP_OK" -eq 0 ]]; then
  echo "FAIL: startup improvement ${STARTUP_IMPROVE}% < ${MIN_STARTUP_IMPROVEMENT_PCT}%" >&2
  exit 1
fi
echo "PASS: >= ${MIN_STARTUP_IMPROVEMENT_PCT}%"

echo ""
echo "All qualification gates passed."
