#!/usr/bin/env bash
# Usage: research/gun/run.sh <batch.json>  — copies the batch to config.json and runs the research rig in agent-5.
set -u
cp "$1" "$(dirname "$0")/config.json"
cd D:/amind/git/astronomical-home && ./scripts/agent_worktree_pool.sh run-tests agent-5 -Mode PlayMode -TestFilter "Tests.PlayMode.Research.GunResearchPlayModeTests.Run" -UnityTimeoutSec 14400
