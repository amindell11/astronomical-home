#!/usr/bin/env bash
set -euo pipefail

# #303: the frame dir is assemble.py's intermediate. It goes only after the clip reads back with the
# expected frame count; --keep-frames or any read-back failure leaves it (and the clip) in place.
# ffmpeg is emulated by fixtures/fake_imageio_ffmpeg, so this needs no wheel and no real encode.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSEMBLE="$SCRIPT_DIR/../capture/assemble.py"
export PYTHONPATH="$SCRIPT_DIR/fixtures/fake_imageio_ffmpeg${PYTHONPATH:+:$PYTHONPATH}"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

PY="python"
command -v "$PY" >/dev/null 2>&1 || PY="python3"
command -v "$PY" >/dev/null 2>&1 || fail "no python on PATH"

make_frames() {
  local dir="$TMP/frames/$1"
  mkdir -p "$dir"
  for i in 0 1 2 3; do : > "$dir/f_0000$i.png"; done
  echo '{"width":64,"height":48,"suggestedFps":10.0}' > "$dir/manifest.json"
  echo "$dir"
}

run() { rc=0; out="$("$PY" "$ASSEMBLE" "$@" 2>&1)" || rc=$?; }

# verified encode: clip stays, frame dir and concat list go
dir="$(make_frames verified)"
run "$dir"
[[ "$rc" -eq 0 ]] || fail "a verified encode must exit 0 (got $rc: $out)"
[[ -f "$dir.mp4" ]] || fail "the clip must be written beside the frame dir"
[[ ! -e "$dir" ]] || fail "a verified encode must delete the frame dir"
[[ ! -e "$dir.mp4.frames.txt" ]] || fail "the concat list must not outlive the encode"

# opt-out: frames are the deliverable
dir="$(make_frames kept)"
run "$dir" --keep-frames
[[ "$rc" -eq 0 ]] || fail "--keep-frames must still exit 0 (got $rc: $out)"
[[ -f "$dir.mp4" ]] || fail "--keep-frames must still write the clip"
[[ -f "$dir/f_00000.png" ]] || fail "--keep-frames must leave the frame dir intact"

# read-back decodes fewer frames than were handed to ffmpeg
dir="$(make_frames short)"
FAKE_FFMPEG_READBACK=2 run "$dir"
[[ "$rc" -ne 0 ]] || fail "a short read-back must exit nonzero (got 0: $out)"
[[ "$out" == *"decoded 2 frames, expected 5"* ]] || fail "the failure must name both counts (got: $out)"
[[ -f "$dir/f_00000.png" ]] || fail "a failed read-back must keep the frame dir"
[[ -f "$dir.mp4" ]] || fail "a failed read-back must keep the clip for inspection"

# read-back cannot decode the clip at all
dir="$(make_frames undecodable)"
FAKE_FFMPEG_READBACK=error run "$dir"
[[ "$rc" -ne 0 ]] || fail "an undecodable clip must exit nonzero (got 0: $out)"
[[ "$out" == *"could not decode"* ]] || fail "the failure must say the decode failed (got: $out)"
[[ -f "$dir/f_00000.png" ]] || fail "an undecodable clip must keep the frame dir"

# the encode itself fails: nothing to verify, nothing deleted
dir="$(make_frames unencoded)"
FAKE_FFMPEG_ENCODE=error run "$dir"
[[ "$rc" -ne 0 ]] || fail "a failed encode must exit nonzero (got 0: $out)"
[[ "$out" == *"ffmpeg failed"* ]] || fail "a failed encode must say so (got: $out)"
[[ -f "$dir/f_00000.png" ]] || fail "a failed encode must keep the frame dir"
[[ ! -e "$dir.mp4.frames.txt" ]] || fail "the concat list must not outlive a failed encode"

echo "PASS: capture/assemble.py — frame dir deleted only after a verified encode"
