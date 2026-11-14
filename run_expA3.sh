#!/usr/bin/env bash
# Experiment A follow-up: why Crimson wins. Same reference and seeds as the rerun; one process a row.
set -u
SLOT=/d/amind/git/agent-2
OUT="D:/amind/git/agent-2/results/expA3"
REF="Vanguard:AggressorPilot:Lasers"
# name|roster token|kits
ROWS=(
  "Crimson-kitsoff|Crimson:AggressorPilot:Lasers|off"
  "Crimson-DefaultShield|Crimson:AggressorPilot:Lasers:Default_Shield|"
  "Crimson-DefaultShield-kitsoff|Crimson:AggressorPilot:Lasers:Default_Shield|off"
  "Valis-kitsoff|Valis:AggressorPilot:Lasers|off"
  "Crimson-LightShield-control|Crimson:AggressorPilot:Lasers:Light_Shield|"
)
cd "$SLOT/training/rl" || exit 2
for rep in 1 2; do
  for entry in "${ROWS[@]}"; do
    IFS='|' read -r name row kits <<< "$entry"
    # The control only has to match the existing row once.
    if [ "$rep" = 2 ] && [ "$name" = "Crimson-LightShield-control" ]; then continue; fi
    dest="$OUT/rep$rep/$name"
    mkdir -p "$dest"
    extra=()
    [ -n "$kits" ] && extra=(--matrix-kits "$kits")
    echo "ROW_START rep=$rep row=$name $(date +%H:%M:%S)"
    .venv/Scripts/python eval_lane.py --seeds 4001,4002,4003,4004,4005 --episodes-per-seed 4 \
      --matrix-reference "$REF" --matrix-roster "$row" "${extra[@]}" \
      --project "$SLOT/src/Asteroids3D" --out-root "$dest" --lease arena-roster-expA \
      > "$dest/launcher.log" 2>&1
    echo "ROW_END rep=$rep row=$name exit=$? $(date +%H:%M:%S)"
  done
done
echo "ALL_DONE"
