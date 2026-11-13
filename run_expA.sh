#!/usr/bin/env bash
# Experiment A: each roster row in its own Unity process, the whole set twice on the same seeds.
set -u
SLOT=/d/amind/git/agent-2
OUT="C:/Users/amind/AppData/Local/Temp/claude/882050741e06a72d/6dc70b3b-499b-4ca8-8077-fa726888596f/scratchpad/expA"
REF="Vanguard:AggressorPilot:Lasers"
ROWS=(
  "Crimson:AggressorPilot:Lasers"
  "Valis:KiterPilot:Lasers"
  "Valis:OrbiterPilot:Lasers"
  "Crimson:KiterPilot:Lasers"
  "Bulwark:OrbiterPilot:TurboLasers"
  "Bulwark:AggressorPilot:TurboLasers"
  "Nightshade:OrbiterPilot:Rippers"
  "Nightshade:AggressorPilot:Rippers"
  "Valis:AggressorPilot:Lasers"
)
cd "$SLOT/training/rl" || exit 2
for rep in 1 2; do
  for row in "${ROWS[@]}"; do
    name=$(echo "$row" | tr ':' '-')
    dest="$OUT/rep$rep/$name"
    mkdir -p "$dest"
    echo "ROW_START rep=$rep row=$row $(date +%H:%M:%S)"
    .venv/Scripts/python eval_lane.py --seeds 4001,4002,4003,4004,4005 --episodes-per-seed 4 \
      --matrix-reference "$REF" --matrix-roster "$row" \
      --project "$SLOT/src/Asteroids3D" --out-root "$dest" --lease arena-roster-expA \
      > "$dest/launcher.log" 2>&1
    echo "ROW_END rep=$rep row=$row exit=$? $(date +%H:%M:%S)"
  done
done
echo "ALL_DONE"
