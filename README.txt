Duel lane, PR for #1133 (fire-plans).
command: eval_lane.py --duel all --seeds 3001,3002,3003 --episodes-per-seed 2
base: d2d2dcd81b384d79f6fbaf4fa2ae1c873cd23a0a (origin/main at build time)
head: the fire-plans tree on that base; its code matches commit a7a84c1fe (later edits before commit: one doc comment and the glossary row)
head-r2: the fire-plans branch at commit b7425c11f (Railgun plan with hysteresis: startAngle 10, breakAngle 30, commitPoint 2/3; fireAngleTolerance back to 2), same command, same base. Files duel-head-r2*.
