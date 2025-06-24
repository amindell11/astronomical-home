#!/usr/bin/env bash
# Test a head-less Unity build for average FPS.
#
# Usage:
#   ./test_headless_perf.sh [--build path] [--arena-count N] [--duration S] [--min-fps N] [--log file]
#
# The script launches the specified build with -batchmode -nographics
# and the custom CLI flags expected by the HeadlessPerfHarness component.
# It then greps the build log for the "PERF_RESULT" line, extracts the
# reported average FPS, prints it, and exits with status 0 if the FPS is
# >= MIN_FPS (default 200) or 1 otherwise.

set -euo pipefail

# Default parameters
BUILD="./LightingTest.exe"
ARENA_COUNT=4
DURATION=30
MIN_FPS=200
LOG="headless.log"

print_help() {
  cat <<EOF
Usage: $0 [options]
  --build PATH        Path to the built Unity executable (default: ./LightingTest.exe)
  --arena-count N     Number of arenas to spawn (default: 4)
  --duration S        Duration to run in seconds (default: 30)
  --min-fps N         Minimum acceptable average FPS (default: 200)
  --log FILE          Log file to write (default: headless.log)
  -h, --help          Show this help and exit
EOF
}

# Parse CLI options
while [[ $# -gt 0 ]]; do
  case "$1" in
    --build)        BUILD="$2"; shift 2 ;;
    --arena-count)  ARENA_COUNT="$2"; shift 2 ;;
    --duration)     DURATION="$2"; shift 2 ;;
    --min-fps)      MIN_FPS="$2"; shift 2 ;;
    --log)          LOG="$2"; shift 2 ;;
    -h|--help)      print_help; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; print_help; exit 1 ;;
  esac
done

# Verify build exists
if [[ ! -x "$BUILD" ]]; then
  echo "Error: build executable '$BUILD' not found or not executable." >&2
  exit 1
fi

echo "Running $BUILD for $DURATION s with $ARENA_COUNT arenas…"

"$BUILD" -batchmode -nographics \
  --arena-count "$ARENA_COUNT" \
  --duration "$DURATION" \
  -logFile "$LOG" || {
    echo "Error: Unity build exited with a non-zero status." >&2
    exit 1
}

# Extract FPS value from log
if ! grep -q "PERF_RESULT" "$LOG"; then
  echo "Error: PERF_RESULT line not found in log ($LOG)." >&2
  exit 1
fi

FPS=$(grep "PERF_RESULT" "$LOG" | tail -n1 | awk -F'=' '{print $2}' | tr -d '[:space:]')

if [[ -z "$FPS" ]]; then
  echo "Error: could not parse FPS value from log." >&2
  exit 1
fi

echo "Average FPS reported: $FPS"

# Compare as floating point using bc (POSIX-compliant)
if command -v bc >/dev/null 2>&1; then
  PASS=$(echo "$FPS >= $MIN_FPS" | bc -l)
else
  # Fallback: chop decimals for integer comparison
  printf -v FPS_INT '%.*f' 0 "$FPS"
  PASS=$(( FPS_INT >= MIN_FPS ? 1 : 0 ))
fi

if [[ "$PASS" -eq 1 ]]; then
  echo "PASS: avg_fps ($FPS) ≥ $MIN_FPS."
  exit 0
else
  echo "FAIL: avg_fps ($FPS) < $MIN_FPS." >&2
  exit 1
fi 