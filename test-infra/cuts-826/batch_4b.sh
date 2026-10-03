#!/usr/bin/env bash
# PR-4b mutation batch: revert slot production, apply named mutations, run the filtered tests, list reds, revert.
# usage: batch_4b.sh <label> <Both|EditMode|PlayMode> <mutation>...
set -u
SLOT=D:/amind/git/agent-6
HERE=D:/amind/git/astronomical-home
label=$1; mode=$2; shift 2
FILTER='MissileGuidancePlayModeTests|MissileLaunchPlayModeTests|ShipChildComponentStatePlayModeTests|LockOnRegistryWiringPlayModeTests|TriggerVolumeActivationPlayModeTests|WeaponCommandDispatchPlayModeTests|RunTallyEditModeTests|RLArenaSeedEditModeTests|MpcIntentSentenceEditModeTests|ObjectiveTrackerEditModeTests|GunsightObservationEditModeTests|DamageControllerEditModeTests|SectorActivationEditModeTests'
cd "$HERE" || exit 2
git -C "$SLOT" checkout -- src/Asteroids3D/Assets src/Asteroids3D/ProjectSettings || exit 2
python reports/test-cuts/mutate_4b.py "$@" || exit 2
rm -rf "$SLOT/src/Asteroids3D/Library/BurstCache/"
echo "=== batch $label: $* ==="
./scripts/agent_worktree_pool.sh run-tests agent-6 -Mode "$mode" -TestFilter "$FILTER" > "reports/test-cuts/4b-$label.log" 2>&1
echo "run-tests exit=$?"
grep -E "STATUS=|error CS" "reports/test-cuts/4b-$label.log" | head -20
PYTHONIOENCODING=utf-8 python reports/test-cuts/reds.py "$SLOT/results/unity-tests-agent"
git -C "$SLOT" checkout -- src/Asteroids3D/Assets src/Asteroids3D/ProjectSettings
git -C "$SLOT" status --short
