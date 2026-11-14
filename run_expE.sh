#!/usr/bin/env bash
# Outnumbered break-off sweep: Vanguard reference against 1-3 Valis or Crimson, break-off threshold swept. One process a row.
set -u
SLOT=/d/amind/git/agent-2
OUT="D:/amind/git/agent-2/results/expE"
REF="Vanguard:AggressorPilot:Lasers"
cd "$SLOT/training/rl" || exit 2
run() { # name, roster token, extra args...
  local name=$1 row=$2; shift 2
  local dest="$OUT/$name"
  mkdir -p "$dest"
  echo "ROW_START row=$name $(date +%H:%M:%S)"
  .venv/Scripts/python eval_lane.py --seeds 4001,4002,4003,4004,4005 --episodes-per-seed 4 \
    --matrix-reference "$REF" --matrix-roster "$row" "$@" \
    --project "$SLOT/src/Asteroids3D" --out-root "$dest" --lease arena-roster-expA \
    > "$dest/launcher.log" 2>&1
  echo "ROW_END row=$name exit=$? $(date +%H:%M:%S)"
}
run "bridge-Crimson-x1-authored" "Crimson:AggressorPilot:Lasers"
run "bridge-Valis-x1-authored" "Valis:AggressorPilot:Lasers"
for enemy in Valis Crimson; do
  for n in 1 2 3; do
    for bo in 0 25 50 75; do
      run "$enemy-x$n-bo$bo" "$enemy:AggressorPilot:Lasers" --matrix-enemies "$n" --matrix-reference-breakoff "$bo"
    done
  done
done
run "repeat-Valis-x2-bo50" "Valis:AggressorPilot:Lasers" --matrix-enemies 2 --matrix-reference-breakoff 50
echo "ALL_DONE"
