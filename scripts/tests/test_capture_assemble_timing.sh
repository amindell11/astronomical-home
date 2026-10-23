#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/capture/assemble.py

# #1103: suggestedFps 50.00001907348633 with --step 2 used to truncate each GIF frame to
# 39 ms, and Pillow then stored 30 ms (2850 ms across 95 frames). Quantising to GIF's
# 10 ms unit yields 40 ms and 3800 ms. #1104: the peak frame on evidence/sector-jump
# keeps its red paint under maximum-coverage quantisation.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSEMBLE="$SCRIPT_DIR/../capture/assemble.py"
FIXTURE="$SCRIPT_DIR/fixtures/capture/original-raw-peak.png"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

PY="python"
command -v "$PY" >/dev/null 2>&1 || PY="python3"
command -v "$PY" >/dev/null 2>&1 || fail "no python on PATH"
[[ -f "$FIXTURE" ]] || fail "missing colour fixture $FIXTURE"

"$PY" - "$TMP/frames/duration" <<'PY'
import sys
from pathlib import Path
from PIL import Image
root = Path(sys.argv[1])
root.mkdir(parents=True)
for index in range(190):
    Image.new("RGB", (2, 2), (index % 255, 0, 0)).save(root / f"f_{index:05d}.png")
(root / "manifest.json").write_text('{"width": 2, "height": 2, "suggestedFps": 50.00001907348633}\n', encoding="utf-8")
PY

rc=0
out="$("$PY" "$ASSEMBLE" "$TMP/frames/duration" --format gif --step 2 --keep-frames 2>&1)" || rc=$?
[[ "$rc" -eq 0 ]] || fail "the #1103 rate must encode (got $rc: $out)"

"$PY" - "$TMP/frames/duration.gif" <<'PY'
import sys
from PIL import Image
gif = Image.open(sys.argv[1])
total = 0
for index in range(gif.n_frames):
    gif.seek(index)
    total += gif.info["duration"]
if gif.n_frames != 95:
    raise SystemExit(f"expected 95 gif frames, got {gif.n_frames}")
if total != 3800:
    raise SystemExit(f"expected 3800 ms, got {total}")
PY

color="$TMP/frames/color"
mkdir -p "$color"
cp "$FIXTURE" "$color/f_00000.png"
printf '%s\n' '{"width": 960, "height": 540, "suggestedFps": 10}' > "$color/manifest.json"

rc=0
out="$("$PY" "$ASSEMBLE" "$color" --format gif --keep-frames 2>&1)" || rc=$?
[[ "$rc" -eq 0 ]] || fail "the #1104 peak must keep its paint (got $rc: $out)"

"$PY" - "$ASSEMBLE" "$FIXTURE" "$color.gif" <<'PY'
import importlib.util
import sys
from PIL import Image
spec = importlib.util.spec_from_file_location("assemble", sys.argv[1])
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
source = Image.open(sys.argv[2]).convert("RGB")
gif = Image.open(sys.argv[3])
gif.seek(0)
error = mod.subject_channel_error(source, gif.convert("RGB"))
if error is None or error >= 8:
    raise SystemExit(f"red-paint error should stay near the measured 2.92, got {error}")
print(f"paint-error {error:.2f}")
PY

echo "PASS: capture/assemble.py — gif duration quantum and subject colour"
