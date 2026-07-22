# Astronomical

A 3D space-combat game built in Unity 6 — asteroid fields, utility-AI enemy ships driven by an MPC (model-predictive control) pilot, and a reinforcement-learning training pipeline (Unity ML-Agents + PPO self-play) for learned combat policies.

## Layout

- `src/Asteroids3D/` — the Unity project (single `GameCore` assembly).
- `training/rl/` — Python ML-Agents training harness (configs, runners, Unity-access coordination). See `training/rl/README.md`.
- `scripts/` — agent tooling: worktree pool, Unity test runner, Unity access coordinator.
- `doc/` — feature plans (`doc/Feature_Plans/`, lifecycle conventions in `AGENTS.md`) and postmortems.
- `art/` — WIP Blender sources and downloaded asset archives, kept outside `Assets/` so Unity never imports them.
- `tools/` — standalone utilities.

## Key docs

- `TESTING.md` — test suite guide and runner usage.
- `AGENTS.md` — design-doc conventions and work-tracking.
- `CLAUDE.md` — agent workflow rules (worktree/PR loop, fix ladder).
