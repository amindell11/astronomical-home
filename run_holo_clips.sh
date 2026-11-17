#!/usr/bin/env bash
# Renders the hologram-entrance section (2.5 s to 5.5 s) for each mode, two processes per mode.
set -u
B="/c/Program Files/Blender Foundation/Blender 5.1/blender.exe"
D="$(cd "$(dirname "$0")" && pwd)"
OUT="$D/anim2"
F=sky,holo,lamps,screens,bloom,guard,header,angle,air,wear
start=$(date +%s)
for m in trace grow dissolve; do
  mkdir -p "$OUT/$m"
  for r in 60:96 96:132; do
    MOCK_YAW=10 MOCK_SAMPLES=16 MOCK_PCT=60 "$B" -b "$D/console-armored.blend" --python "$D/dress.py" -- \
      "$OUT/$m" "$F,engine=eevee,anim=24x6,frames=$r,holo=$m" > "$OUT/$m-${r%%:*}.log" 2>&1 &
  done
done
wait
for m in trace grow dissolve; do echo "$m: $(ls "$OUT/$m" | wc -l) frames"; done
echo "secs=$(( $(date +%s) - start ))"
grep -h -E "Traceback|Error:" "$OUT"/*.log | grep -v "strokes set empty\|HIPEW" | sort -u
