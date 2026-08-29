#!/usr/bin/env bash
set -euo pipefail

# Regression (#456): assemble.py must not report success when it produced no clip. A frameless
# directory used to print "skip" and exit 0, so a caller waiting for footage got a green run and
# nothing to show. Frameless dirs need no ffmpeg/PIL, so this stays dependency-free.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSEMBLE="$SCRIPT_DIR/../capture/assemble.py"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

PY="python"
command -v "$PY" >/dev/null 2>&1 || PY="python3"
command -v "$PY" >/dev/null 2>&1 || fail "no python on PATH"

mkdir -p "$TMP/frames/empty-clip"

rc=0
out="$("$PY" "$ASSEMBLE" "$TMP/frames/empty-clip" 2>&1)" || rc=$?
[[ "$rc" -ne 0 ]] || fail "a frameless directory must exit nonzero (got 0: $out)"
[[ "$out" == *"no clips assembled"* ]] || fail "the failure must say no clip was produced (got: $out)"

rc=0
out="$("$PY" "$ASSEMBLE" "$TMP/frames/nothing-matches-this-*" 2>&1)" || rc=$?
[[ "$rc" -ne 0 ]] || fail "a glob matching no directory must exit nonzero (got 0: $out)"
[[ "$out" == *"no frame directories matched"* ]] || fail "an unmatched glob must say so (got: $out)"

echo "PASS: capture/assemble.py — zero assembled clips exits nonzero"
