#!/usr/bin/env bash
# Light shield candidates on Crimson: against the Vanguard reference and against Valis directly. One process a row.
set -u
SLOT=/d/amind/git/agent-2
OUT="D:/amind/git/agent-2/results/expS"
CR="Crimson:AggressorPilot:Lasers"
VAN="Vanguard:AggressorPilot:Lasers"
VAL="Valis:AggressorPilot:Lasers"
# name|reference|roster token
ROWS=(
  "vsVanguard-Light-control|$VAN|$CR:32/2/18"
  "vsVanguard-A|$VAN|$CR:32/2/10"
  "vsVanguard-B|$VAN|$CR:24/2/18"
  "vsVanguard-C|$VAN|$CR:28/2/12"
  "vsValis-Light|$VAL|$CR:Light_Shield"
  "vsValis-A|$VAL|$CR:32/2/10"
  "vsValis-B|$VAL|$CR:24/2/18"
  "vsValis-C|$VAL|$CR:28/2/12"
  "vsVanguard-A-repeat|$VAN|$CR:32/2/10"
)
cd "$SLOT/training/rl" || exit 2
for entry in "${ROWS[@]}"; do
  IFS='|' read -r name ref row <<< "$entry"
  dest="$OUT/$name"
  mkdir -p "$dest"
  echo "ROW_START row=$name $(date +%H:%M:%S)"
  .venv/Scripts/python eval_lane.py --seeds 4001,4002,4003,4004,4005 --episodes-per-seed 4 \
    --matrix-reference "$ref" --matrix-roster "$row" \
    --project "$SLOT/src/Asteroids3D" --out-root "$dest" --lease arena-roster-expA \
    > "$dest/launcher.log" 2>&1
  echo "ROW_END row=$name exit=$? $(date +%H:%M:%S)"
done
echo "ALL_DONE"
