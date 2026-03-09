---
name: ai-utility-analysis
description: Analyze AI utility decision logs from the UtilityLogger system. Run Python analysis scripts on JSONL logs to find state oscillation, transition patterns, factor influence, and behavioral anomalies.
---

# AI Utility Analysis

Use this skill to study AI ship behavior from logged utility decisions.

## Architecture

### Logging (C# — runtime)

**`UtilityLogger`** (`Scripts/AI/Debug/UtilityLogger.cs`)
- MonoBehaviour, attach to any GameObject with `AICommander`
- Guarded by `#if UNITY_EDITOR || DEBUG`
- Writes JSONL to `Application.persistentDataPath/AILogs/[sessionTag]/`
  - Windows default: `%APPDATA%/../LocalLow/AryeGames/Astronomical/AILogs/`
- Configurable: `tickInterval` (default 5 = ~10Hz), `alwaysLogTransitions`, `includeFactorBreakdowns`, `sessionTag`

**Log entry format** (one JSON object per line):
```json
{
  "t": 1.5,
  "ship": "Team0_3",
  "state": "Attack",
  "tick": 15,
  "ctx": {"hp":1.0, "shield":0.8, "inCombat":true, "enemyDist":12.5, "enemyHp":0.9, "enemyShield":0.7, "los":true, "enemies":2, "friends":1, "closing":3.2, "missile":false, "angle":45.0},
  "scores": {"Attack":1.47, "Patrol":0.01, "Evade":0.82, ...},
  "factors": {"Attack":{"selfHealth":0.95, "range":1.1, "LOS":1.2, ...}, ...},
  "transition": {"from":"Patrol", "to":"Attack"}
}
```

### Plumbing (C# — how it works)

- `State.NewBuilder()` creates a `UtilityBuilder` and stores it as `State.LastBuilder`
- All states (Attack, Patrol, Evade, Kite, Orbit, JinkEvade) use `NewBuilder()` instead of `new UtilityBuilder()`
- `UtilityBuilder.Factors` exposes `IReadOnlyList<(string name, float value)>` (editor/debug builds)
- `UtilityBuilder.Result` stores the final geometric-mean after `Build()`
- `UtilitySelector.RegisteredStates` exposes all states publicly
- `UtilitySelector.OnStateTransition` event fires on every transition

### Analysis scripts (Python — offline)

**`scripts/ai-analysis/analyze_utility.py`** — Primary analysis tool:
```
python scripts/ai-analysis/analyze_utility.py <path> [--ship NAME] [--top N]
                                                      [--transitions] [--factors]
                                                      [--switching] [--timeline]
```
- `<path>`: single `.jsonl` file or directory (globs all `.jsonl`)
- Analyses: state distribution, transition matrix + context, avg utility scores, factor variance ranking, rapid switching detection (oscillation finder), combat engagement timeline

**`scripts/ai-analysis/find_patterns.py`** — Deeper pattern mining:
```
python scripts/ai-analysis/find_patterns.py <path> [--ship NAME]
                                                     [--boundaries] [--gaps]
                                                     [--durations] [--state NAME]
                                                     [--compare SHIP_A SHIP_B]
```
- Decision boundaries (context at each transition type)
- Score gap analysis (close calls vs dominant decisions)
- State duration stats (how long states last before switching)
- Per-state factor deep-dive (`--state Attack`)
- Ship-vs-ship comparison

## Workflow

1. **Collect logs**: Play the arena scene with `UtilityLogger` attached to AI ships. Run for 30-60+ seconds.
2. **Run broad analysis**:
   ```
   python scripts/ai-analysis/analyze_utility.py <log_dir>
   ```
3. **Check for oscillation**: Look at "Rapid Switching Detection" — 4+ transitions in 5s window = problem.
4. **Investigate transitions**: Use `--transitions` to see context at switch points. If Attack→Patrol and Patrol→Attack contexts are nearly identical, the switching is spurious.
5. **Drill into factors**: Use `find_patterns.py --state Attack` to see which factors drive/suppress a state.
6. **Compare ships**: Use `--compare Ship_A Ship_B` to spot personality differences (or lack thereof).

## Key findings from initial analysis

- **Attack↔Patrol oscillation**: All ships flip between Attack and Patrol every ~0.7s. Root cause: `Combat.InCombat` flickers because `Combat.Enemy` has zero target stickiness — drops the cached enemy the instant it leaves the 30m OverlapSphere scan radius. Patrol's binary utility (2.0 vs 0.01) amplifies the flicker into full state oscillation.
- **Fix needed**: Target stickiness in `AI.Context.Combat` — once an enemy is acquired, keep tracking until truly lost (dead/despawned/well beyond range). This is upstream of any utility tuning.
- **Factor influence**: `selfShield` and `LOS` are the highest-variance Attack factors. `threat` and `range` matter but swing less.
- **All ships behave identically**: No personality differentiation yet (same UtilityTuning, same UtilityWeights).

## Key files

| File | Role |
|------|------|
| `Scripts/AI/Debug/UtilityLogger.cs` | Runtime JSONL logger |
| `Scripts/AI/States/State.cs` | `NewBuilder()` + `LastBuilder` property |
| `Scripts/AI/Utility/Editor/UtilityBuilder.Editor.cs` | `Factors`, `Result` accessors |
| `Scripts/AI/Utility/UtilitySelector.cs` | `RegisteredStates`, `OnStateTransition` event |
| `Scripts/AI/Context/Combat.cs` | `InCombat` / `Enemy` — target acquisition (needs stickiness fix) |
| `scripts/ai-analysis/analyze_utility.py` | Primary analysis script |
| `scripts/ai-analysis/find_patterns.py` | Pattern mining script |
