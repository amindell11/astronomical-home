Railgun duel, fire plan x AI skill, for PR #1138 (arc #409). Measurement only, not for merge.

command (per cell, from the cell's build in pool slot agent-13):
  training/rl/eval_lane.py --duel Railgun --seeds 3001,3002,3003 --episodes-per-seed 2 --out-root <scratch>/<cell> --lease railgun-skill-<cell>
targets: Dummy (still) and Orbiter (crossing); 6 episodes each, 60 sim s timeout per episode.
ships: both Crimson with their Blink kit, as the lane flies them; shooter and target pilots are TestPilotMPC, which uses AimSettings_AIPilot.

base: main c84645f6fe37d329a20b8805520b3d019162f4d3 (Railgun chargeTime 1.1 s, #1135)
#1062 aim servo: task/aim-servo ace5e62597565fa0600f45ae744c205ff7792c45
#1138 fire plans: task/fire-plans b7425c11f6239595fca6a057529b19e3d107b0e0

builds:
  old-low   evidence/railgun-skill-old-low   74863c188e74e16f740e7e69c54fd9c486bc3c8b  main + merge of #1062
  old-high  evidence/railgun-skill-old-high  a18614f86cb36cd9db593e4dade2771bde2ceac9  old-low + aim asset edit
  new-low   evidence/railgun-skill-new-low   5f0dad5332c8f97146a31ee8b9ffbff013c4bd76  old-low + merge of #1138
  new-high  evidence/railgun-skill-new-high  d25aa564fe32de190ee8fc9d32495a15f44d9796  new-low + aim asset edit

old plan: Railguns.ShouldFire = InEnvelope: hold the charge trigger only while LOS, range <= 45 and angle <= fireAngleTolerance 2 deg.
new plan: Railguns.HoldTrigger: start within startAngle 10 deg, drop past breakAngle 30 deg before commitPoint 2/3, hold to full after it; leaving 45 u drops at any point.

AimSettings_AIPilot (Assets/Settings/AI/Aim):
  low  (as authored on #1062): reactionDelay 0.375, wanderSize 11.25, wanderCorrelationTime 0.5, startingError 0 (earned accuracy off)
  high: reactionDelay 0, wanderSize 0, everything else unchanged
MpcSettings_AgentPilot aimServoAccelFraction 0.21 in every cell.

layout:
  compare.md                    the four-cell table
  duel-lane/<cell>/             probe json/md/jsonl, lane summary, episode jsonl, editor.log
  tests/<build>-summary.json    Weapons;AI category run (-Mode Both) per build, with the PlayMode xml
