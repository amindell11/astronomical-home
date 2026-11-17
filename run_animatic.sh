#!/usr/bin/env bash
# Renders the settle-in + power-on animatic in parallel frame ranges, then encodes an MP4.
# Usage: run_animatic.sh <out_dir> <fps> <seconds> <procs> <pct>
set -u
B="/c/Program Files/Blender Foundation/Blender 5.1/blender.exe"
D="$(cd "$(dirname "$0")" && pwd)"
OUT="$1"; FPS="$2"; SECS="$3"; PROCS="$4"; PCT="$5"
F=sky,holo,lamps,screens,bloom,guard,header,angle,air,wear
mkdir -p "$OUT/frames"
TOTAL=$(python -c "print(int($FPS*$SECS))")
CHUNK=$(( (TOTAL + PROCS - 1) / PROCS ))
start=$(date +%s)
for ((p=0; p<PROCS; p++)); do
  lo=$(( p * CHUNK )); hi=$(( lo + CHUNK ))
  MOCK_YAW=10 MOCK_SAMPLES=16 MOCK_PCT=$PCT "$B" -b "$D/console-armored.blend" --python "$D/dress.py" -- \
    "$OUT/frames" "$F,engine=eevee,anim=${FPS}x${SECS},frames=$lo:$hi${EXTRA:+,$EXTRA}" > "$OUT/part$p.log" 2>&1 &
done
wait
echo "rendered $(ls "$OUT/frames" | wc -l) of $TOTAL frames in $(( $(date +%s) - start ))s"
grep -h -E "Traceback|Error:" "$OUT"/part*.log | grep -v "strokes set empty\|HIPEW" | sort -u
uv run -q --with "imageio[ffmpeg]" python - "$OUT" "$FPS" <<'PY'
import sys, glob, imageio.v2 as iio
out, fps = sys.argv[1], int(sys.argv[2])
frames = sorted(glob.glob(out + "/frames/f*.png"))
with iio.get_writer(out + "/hangar-entry-animatic.mp4", fps=fps, codec="libx264", quality=8, pixelformat="yuv420p",
                    macro_block_size=8) as w:
    for f in frames:
        w.append_data(iio.imread(f)[:, :, :3])
print("encoded", len(frames), "frames")
PY
