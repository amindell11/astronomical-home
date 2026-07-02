---
name: ai-utility-analysis
description: Analyze AI ship decision-making by logging utility scores to JSONL at runtime and mining them offline. Use when investigating AI behavior problems — state oscillation, spurious transitions, factor influence, or ship-vs-ship differences in the utility-based AI.
---

# AI Utility Analysis

Debug why AI ships pick the states they do. Two stages: **log utility
decisions to JSONL in play mode**, then **crunch the logs offline in Python**.

> Verified against the codebase 2026-07-01. If a type/path below doesn't
> resolve, the AI subsystem may have been refactored again — grep for the
> symbol under `Assets/Scripts/AI/` (mainly `AI/Strategy/` + `AI/Editor/`)
> rather than trusting these paths.

## Stage 1 — Capture logs (C#, Editor only)

`UtilityLogger` (`Assets/Scripts/AI/Editor/UtilityLogger.Editor.cs`) is a
`MonoBehaviour`, gated by `#if UNITY_EDITOR`. To get output, **both** must hold:

1. The GameObject has an `AICommander`, and you attach `UtilityLogger` to it.
2. The commander's `AIDebugSettings` asset
   (`AICommander.DebugSettings`, a `ScriptableObject` — create via menu
   **AI/Debug Settings**) has the **`Logging`** channel enabled
   (`AIDebugChannel.Logging`, bit `1 << 6`). Without it the logger disables
   itself in `Start()`.

Logger inspector fields: `tickInterval` (default 5 ≈ log every 5th
FixedUpdate), `alwaysLogTransitions` (log on every state switch regardless of
interval), `includeFactorBreakdowns`, `sessionTag` (subfolder), `flushThreshold`.

Output path: `Application.persistentDataPath/AILogs/[sessionTag]/{ship}_{timestamp}.jsonl`
- Windows: `%APPDATA%/../LocalLow/AryeGames/Astronomical/AILogs/`
- The `Start()` log line prints the exact file path (`[UtilityLogger] Logging to: ...`).

**One JSON object per line:**
```json
{
  "t": 1.500, "ship": "Team0_3", "state": "Attack", "tick": 15,
  "ctx": {"hp":1.0,"shield":0.8,"inCombat":true,"enemyDist":12.5,"enemyHp":0.9,
          "enemyShield":0.7,"los":true,"enemies":2,"friends":1,"closing":3.2,
          "missile":false,"angle":45.0},
  "scores": {"Attack":1.470, "Patrol":0.010, "Evade":0.820},
  "factors": {"Attack": {"selfHealth":0.95, "range":1.1, "LOS":1.2}},
  "transition": {"from":"Patrol", "to":"Attack"}
}
```
- `scores`: utility of every candidate state that tick.
- `factors`: per-factor breakdown behind each state's score (from
  `Sampler.GetBuilder(state).Factors`); present only when
  `includeFactorBreakdowns` is on.
- `transition`: present only on ticks where a state switch occurred.
- State names are `AIState.ProfileName`.

## Stage 2 — Analyze logs (Python, offline)

Both scripts take a single `.jsonl` file or a directory (globs all `*.jsonl`).

**`scripts/ai-analysis/analyze_utility.py`** — broad report:
```
python scripts/ai-analysis/analyze_utility.py <path> [--ship NAME] [--top N]
       [--transitions] [--factors] [--switching] [--timeline]
```
State distribution · transition matrix + context at switch points · avg utility
scores · factor-variance ranking · **rapid-switching / oscillation detection**
· combat-engagement timeline.

**`scripts/ai-analysis/find_patterns.py`** — deeper mining:
```
python scripts/ai-analysis/find_patterns.py <path> [--ship NAME]
       [--boundaries] [--anomalies] [--threshold X] [--compare SHIP_A SHIP_B]
```
Decision boundaries (context at each transition type) · score-gap "close calls"
vs dominant decisions · state durations · per-state factor deep-dive ·
ship-vs-ship comparison.

## Workflow

1. **Enable + attach**: turn on the `Logging` channel on the AI's
   `AIDebugSettings`, attach `UtilityLogger`, play the arena scene 30–60s.
2. **Broad pass**: `analyze_utility.py <log_dir>`.
3. **Oscillation check**: read "Rapid Switching Detection" — 4+ transitions in a
   5s window on one ship is a problem. If `A→B` and `B→A` fire with nearly
   identical `ctx`, the switching is spurious (upstream data flicker, not
   tuning).
4. **Drill factors**: `find_patterns.py --boundaries` (or per-state) to see
   which factors drive vs suppress a state.
5. **Compare ships**: `--compare Ship_A Ship_B` to spot personality
   differences — or confirm there are none (same tuning ⇒ identical behavior).

## Key files

| File | Role |
|------|------|
| `Assets/Scripts/AI/Editor/UtilityLogger.Editor.cs` | Runtime JSONL logger (Editor-only) |
| `Assets/Scripts/AI/Editor/AIDebugSettings.Editor.cs` | `AIDebugChannel` enum incl. `Logging`; the SO that gates output |
| `Assets/Scripts/AI/Editor/AICommander.Editor.cs` | `DebugSettings` accessor |
| `Assets/Scripts/AI/Strategy/` | `UtilityChooser`, `Sampler`, `UtilityBuilder`, `AIState` — the utility system the logger reads |
| `scripts/ai-analysis/analyze_utility.py` | Broad analysis |
| `scripts/ai-analysis/find_patterns.py` | Pattern mining |
