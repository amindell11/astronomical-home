#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/capture/assemble.py

# suggestedFps 50.00001907348633 with --step 2 quantises to 40 ms per GIF frame,
# 3800 ms across 95 frames. The peak fixture must keep its red paint.

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
[[ "$rc" -eq 0 ]] || fail "the fractional manifest rate must encode (got $rc: $out)"

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
[[ "$rc" -eq 0 ]] || fail "the saturated ship must keep its paint (got $rc: $out)"

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

echo "PASS: capture/assemble.py - gif duration quantum and subject colour"

repeated="$TMP/frames/repeated"
mkdir -p "$repeated"
cp "$FIXTURE" "$repeated/f_00000.png"
cp "$FIXTURE" "$repeated/f_00001.png"
out="$("$PY" "$ASSEMBLE" "$repeated" --format gif --keep-frames 2>&1)" || fail "repeated source frames must encode: $out"
rc=0
out="$("$PY" "$ASSEMBLE" "$repeated" --format gif --colors 2 2>&1)" || rc=$?
[[ "$rc" -ne 0 && "$out" == *"subject colour error"* ]] || fail "coalesced frames must reject lost subject colour: $out"
[[ -f "$repeated/f_00000.png" && -f "$repeated/f_00001.png" && -f "$repeated.gif" ]] || fail "failed colour read-back must keep clip and source frames"

echo "PASS: capture/assemble.py - coalesced frames still verify subject colour"

"$PY" - "$ASSEMBLE" "$TMP/frames/short-intervals" <<'PY'
import subprocess
import sys
from pathlib import Path

import imageio_ffmpeg
from PIL import Image

script, directory = sys.argv[1:]
root = Path(directory)
root.mkdir(parents=True)
colors = [(30 + i % 4 * 60, 30 + i // 4 % 4 * 60, 30 + i // 16 * 120) for i in range(24)]
for index, color in enumerate(colors):
    Image.new("RGB", (16, 16), color).save(root / f"f_{index:05d}.png")
subprocess.run([sys.executable, script, str(root), "--fps", str(1 / 0.015), "--keep-frames"], check=True)
clip = str(root) + ".mp4"
ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
raw = subprocess.run([
    ffmpeg, "-v", "error", "-i", clip, "-f", "rawvideo", "-pix_fmt", "rgb24",
    "-fps_mode", "passthrough", "-",
], capture_output=True, check=True).stdout
decoded_ids = [
    min(range(len(colors)), key=lambda index: sum(abs(raw[offset + channel] - colors[index][channel])
                                                for channel in range(3)))
    for offset in range(0, len(raw), 16 * 16 * 3)
]
expected_ids = list(range(len(colors))) + [len(colors) - 1]
if decoded_ids != expected_ids:
    raise SystemExit(f"short intervals lost or reordered source frames: {decoded_ids}")
PY

echo "PASS: capture/assemble.py - high --fps preserves every source frame"
