#!/usr/bin/env bash
# Unwrap, bake and export one station kind's three dressed models.
# dress_pipeline.sh <builder kind> <art name> <Unity name>
set -euo pipefail
KIND="$1"; ART="$2"; NAME="$3"
S="C:/Users/amind/AppData/Local/Temp/claude/882050741e06a72d/7333cce7-bc83-4523-8f8d-c6070660c379/scratchpad"
SLOT="D:/amind/git/agent-2"
B="/c/Program Files/Blender Foundation/Blender 5.1/blender.exe"
declare -A HATCH=([shoal]=0.24 [veil]=0.08 [bastion]=0.14)
declare -A TITLE=([shoal]=Shoal [veil]=Veil [bastion]=Bastion)
for F in shoal veil bastion; do
  SRC="$S/build/$KIND-$F-kit.blend"
  DIR="$SLOT/art/stations/$ART/$F"
  DST="$DIR/$ART-$F.blend"
  UNITY="$SLOT/src/Asteroids3D/Assets/Visuals/Environment/Stations/$NAME/${TITLE[$F]}"
  mkdir -p "$DIR" "$UNITY" "$S/paint"
  cp "$SRC" "$DST"
  echo "== $KIND $F uv"
  "$B" -b --factory-startup --python-exit-code 1 -P "$SLOT/art/tools/station/station_uv.py" -- --station "$DST"
  echo "== $KIND $F paint"
  "$B" -b --factory-startup --python-exit-code 1 -P "$SLOT/art/tools/station/station_paint.py" -- \
    --station "$DST" --out "$S/paint" --unity "$UNITY" --hatch "${HATCH[$F]}"
  echo "== $KIND $F export"
  "$B" -b --factory-startup --python-exit-code 1 --python "$S/tools/gate4d-export_station.py" -- \
    "$DST" "$UNITY/$NAME ${TITLE[$F]}.fbx" 5
  echo "== $KIND $F done"
done
