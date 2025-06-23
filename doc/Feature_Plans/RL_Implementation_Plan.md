# Dogfight **AIsteroids** – RL Implementation Plan  
*Version 0.2 • June 22 2025*

> **Changelog (v0.2)**  
> • Terminology aligned with current codebase (BT‐driven `AIShipInput`, no `UtilityState` yet).  
> • Clarified observation variables that already exist in scripts.  
> • Timeline compressed to finish by **25 Jun 2025**.  
> • Added § 9 “Codebase Preparation Tasks” outlining required refactors _before_ RL work starts.

> **Purpose**  
> Provide a clear, step-by-step roadmap for hybrid RL control layer, test it in Unity 6000.1.8f1/ML-Agents 3.x, and iterate rapidly.

---

## 1  High-Level Goals
| ID | Goal | Success Metric |
|----|------|----------------|
| G-1 | **Hybrid control** — RL chooses or tunes existing behavior states; scripts handle low-level kinematics. | ≥ 70 % win-rate vs current scripted AI after 3 M training steps. |
| G-2 | **Fast iteration** — reach playable results in ≤ 2 hours wall-clock on an RTX 4080. | End-to-end training time recorded by mlagents-learn log. |
| G-3 | **Robustness** — policy holds up across asteroid densities, weapon balancing, 0.5 ×–2 × base level. | Win-rate drop ≤ 10 % when density varies in eval curriculum tier. |

---

## 2  System Architecture

### 2.1  Runtime Layering (matches current codebase)
1. **RL Arbiter (`RLArbiter` – inherits _ML-Agents_ `Agent`)**
   * Runs every *k* FixedUpdate ticks (start with *k = 4*).
   * Reads compact observation vector (see § 3.1).
   * Emits one **discrete** action: `BTStateIndex ∈ [0, 3]` that maps to existing Behaviour-Tree root states: `Idle`, `Patrol`, `Evade`, `Attack`.
2. **Behaviour-Tree (Unity.Behavior)**
   * Already drives decision-making through nodes under `AIShipInput`.
   * The selected root state activates/deactivates sub-trees via a blackboard enum (`AIShipBehaviorStates`).
3. **AIShipInput + Movement Pipeline**
   * BT leaf nodes call `AIShipInput.SetNavigation*` helpers which ultimately delegate to `PathPlanner` → `VelocityPilot` → `ShipMovement`.
   * No changes needed here; RL only influences the high-level BT state.
4. **(Optional) Fine-grained Tuning Head**
   * A second continuous head can output `[thrust, strafe]` deltas that are summed with `VelocityPilot` output if root state is `Attack`.

---

## 3  ML-Agents Specification

### 3.1  Observations (size ≈ 32)
| Group | Variables (examples) |
|-------|----------------------|
| Self | normalized speed, shield %, heat %, ammo %. |
| Enemy | relative bearing, distance / maxEngageRange, enemy shield %. |
| Environment | nearest asteroid distance × 3, relative velocity to nearest asteroid, time-to-impact, current asteroid density tier (one-hot). |
| Misc | last chosen mode (one-hot), time since last hit, cool-down timers. |

### 3.2  Actions
* **Discrete**: `stateIndex ∈ [0, 3]` mapping to `Idle`, `Patrol`, `Evade`, `Attack`.  **-or-**  
* **Continuous** (optional 2nd head): `[Δthrust, Δstrafe]` clamped to [-1,1] added to `VelocityPilot` output when `stateIndex == Attack`.

### 3.3  Reward Function

R_t = +1 ⋅ enemy_destroyed
–1 ⋅ self_destroyed
+0.2 ⋅ (enemy_damage_dealt)
–0.1 ⋅ (self_damage_taken)
–0.01⋅ (asteroid_near_miss_penalty)

* Dense shaping helps the Arbiter learn even though movement scripts fly the ship.

### 3.4  Curriculum
| Tier | Asteroid Density | Enemy AI | Episodes |
|------|------------------|----------|----------|
| 0    | 25 % baseline    | Dummy    | 5 k |
| 1    | 50 %             | Dummy    | 10 k |
| 2    | 100 % (baseline) | Dummy    | 20 k |
| 3    | 100 %            | Original Scripted AI | 20 k |
| 4    | 150 %            | Scripted | 10 k |

Start each tier once mean reward ≥ 0.3 for 100 % baseline tier-1 moving average.

---

## 4  Training Pipeline

1. **Behavioral Cloning Bootstrap**  
   * Record 100 k frames of current scripted AI (`--recording` flag).  
   * Pretrain network with BC loss (ML-Agents `--init-path`).

2. **PPO Fine-Tuning**  
   * `buffer_size = 40960`, `batch_size = 1024`, `learning_rate = 2.5e-4`, `entropy = 0.01`.  
   * Use parameter-noise exploration (`use_recurrent = false`, `normalize = true`).

3. **Checkpoints & Metrics**  
   * Save every 50 k steps to `/Models/Checkpoints/`.  
   * Auto-eval script logs win-rate, avg reward, and mode-usage histogram.

4. **Hyperparameter Sweep (optional)**  
   * Grid over `entropy` {0.005, 0.01, 0.02} × `learning_rate` {2.5e-4, 5e-4}.  
   * Use Sacred or Weights & Biases for tracking.

---

## 5  Unity Integration Tasks

| # | Task | Owner | Notes |
|---|------|-------|-------|
| U-1 | Create `RLArbiter.cs` MonoBehaviour | ✅ **COMPLETE (Jun 22)** | Implements ML-Agents Agent; writes selected state to `BTStateBridge`. |
| U-2 | Ensure `AIShipBehaviorStates` enum is public & serializable | ✅ **COMPLETE (Jun 22)** | Required for RL action mapping and blackboard writes. |
| U-3 | Hook `AsteroidFieldManager` to EnvParams (`AsteroidDensity`) | — | `Academy.Instance.EnvironmentParameters`. |
| U-4 | Implement training/eval scene (`RLTrainingArena.unity`) | — | Head-less, time-scaled ×10. |
| U-5 | Build evaluation dashboard (Editor Window) | — | Plots win-rate vs checkpoint. |

---

## 6  Evaluation Plan

1. **Offline validation** after each tier: 1 k episodes × 3 seeds.  
2. **Stress tests**: vary asteroid count (0.2×–3×), missile speed (0.8×–1.2×).  
3. **User play-test**: designers fight the RL bot; collect qualitative feedback on "fun" & perceived skill.

---

## 7  Timeline (compressed – finish by 25 Jun 2025)

| Date (EOD) | Deliverable |
|------------|-------------|
| **23 Jun** | • Import ML-Agents 3.x package & verify compile with existing asmdefs  <br/>• Scaffold `RLArbiter.cs` with observation & action stubs  <br/>• Add `ShipEvents.cs` (OnDestroyed, OnDealDamage) for reward hooks |
| **24 Jun** | • Record 100 k BC frames using current AI (`--record`)  <br/>• Implement `RLTrainingArena.unity` (head-less, time-scaled ×10)  <br/>• Finish observation writer, reward function, and environment-parameter hooks (`AsteroidDensity`) |
| **25 Jun** | • Run BC → PPO Tier-0 training (≤ 3 h on RTX 4080)  <br/>• Evaluate vs scripted AI for 500 episodes; target ≥ 0 win-rate  <br/>• Ship README + demo video/gif of RL bot defeating scripted AI |

---

## 8  Open Questions / Risks

* **Observation budget:** 32 floats OK for performance?  
* **State granularity:** Are 4 root states sufficient or should we split `Evade` into `AsteroidAvoid` vs `DogfightEvade`?  
* **Continuous tuning head:** may complicate credit assignment—disable if convergence stalls.  
* **ML-Agents 3.x compatibility:** confirm multi-behavior spec works in 2023.2.

Please add comments inline or open Git issues referencing section IDs above.

---

## 9  Codebase Preparation Tasks (must complete before § 4 Training Pipeline)

| ID | Area | File(s) | Change |
|----|------|---------|--------|
| C-1 | **Events for rewards** | `ShipDamageHandler.cs`, `Asteroid.cs` | Expose `OnDestroyed`, `OnDamageDealt` events so `RLArbiter` can subscribe without reflection. |
| C-2 | **Observation API** | new `ShipObservation.cs` | Provide static helpers to read: speed %, shield %, relative bearings, nearest asteroid info using existing `AsteroidFieldManager`. |
| C-3 | **BT State Bridge** | new `BTStateBridge.cs` | Map `int action → AIShipBehaviorStates` and write to Blackboard; read current state for observations. |
| C-4 | **Environment Parameters** | `AsteroidFieldManager.cs` | Replace `targetVolumeDensity` with value from `Academy.Instance.EnvironmentParameters`. |
| C-5 | **asmdef Updates** | `Game.Core.asmdef`, `Game.AI.asmdef` | Add reference to `Unity.ML-Agents` assembly. |
| C-6 | **Training Scene** | `RLTrainingArena.unity` | Minimal scene containing: player prefab (Agent), dummy enemy/asteroid spawners, `Academy`, `Environment`. |
| C-7 | **Build Scripts** | new `Editor/TrainingMenu.cs` | One-click menu item _"Run RL Training"_ that loads arena & starts recording. |
| C-8 | **Performance Clean-up** | remove/guard `Debug.Log` in hot paths (`MissileProjectile`, `AsteroidSpawner`) behind `#if UNITY_EDITOR`. |
| C-9 | **CI Hook** | `.github/workflows/train.yml` | Optional – run short inference test to ensure model loads without errors.

_Complete C-1 … C-4 on **23 Jun**, C-5 … C-7 on **24 Jun**; C-8 & C-9 are stretch goals._
