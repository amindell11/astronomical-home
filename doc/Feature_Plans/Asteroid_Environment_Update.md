# Asteroid Environment Update – Camera-Free, Multi-Arena Support  
*Version 0.1 • 22 Jun 2025*

> **Purpose**  
> Prepare the asteroid spawning & arena code so it can be instantiated multiple times in a single head-less build (or across subprocesses) without relying on `Camera.main`.

---

## 1  Motivation
* ML-Agents training uses head-less builds – they have no `Camera.main`.
* Running N arenas in one process multiplies sample throughput without extra GPUs.
* Current `AsteroidFieldManager` ties spawn logic to the player-camera, making parallel arenas impossible.

---

## 2  Goals
| ID | Goal | Acceptance Test |
|----|------|----------------|
| G-1 | Asteroid spawns use a configurable anchor transform, not the camera. | Arena prefab sets `spawnAnchor = Agent.transform` and rocks appear correctly. |
| G-2 | Arena prefab is self-contained & relocatable. | Instantiating two prefabs at different positions spawns independent fields. |
| G-3 | Head-less build runs ≥ 4 arenas in one process at ≥ 200 FPS (RTX 4080). | Profiler capture. |

---

## 3  Change List
| ID | Area | File(s) | Change |
|----|------|---------|--------|
| C-1 | **Anchor decoupling** | `AsteroidFieldManager.cs` | Add serialized `spawnAnchor` + `SetAnchor()`. Replace `Camera.main` / `playerTransform` with `AnchorPos`. |
| C-2 | **Editor safety** | same | Guard `Gizmos` & `Debug.Log` with `#if UNITY_EDITOR`. |
| C-3 | **Arena prefab** | `Assets/Prefabs/RLTrainingArena.prefab` | Contains AgentShip, EnemyShip, AsteroidFieldManager, Boundaries. All children local-space. |
| C-4 | **Arena reset** | `ArenaReset.cs` (new) | Handles episode reset: clears asteroids, respawns ships, signals ML-Agents. |
| C-5 | **Multi-arena spawner** | `ArenaSpawner.cs` (new) | Grid-instantiates N prefabs when `Application.isBatchMode`. |
| C-6 | **Camera stripping** | `CameraFollow.cs` | Disable component in training builds (`#if UNITY_EDITOR`). |
| C-7 | **CI/Perf** | `.github/workflows/train.yml` | Add play-mode test that instantiates 4 arenas & steps 1 000 frames. |

---

## 4  Implementation Steps
1. **Day 1 (23 Jun)**  
   a. Implement C-1, C-2 ➜ commit.  
   b. Unit-test single arena in Play-Mode.
2. **Day 1 (23 Jun)**  
   c. Build `RLTrainingArena.prefab` (C-3).  
   d. Write `ArenaReset.cs` and attach.  
   e. Verify one-arena Episode resets.
3. **Day 2 (24 Jun)**  
   f. Implement `ArenaSpawner.cs` (C-5); test 4-arena scene in Editor & head-less (`-batchmode`).  
   g. Wrap `CameraFollow` behaviour (C-6).
4. **Stretch**  
   h. Add CI perf test (C-7).

---

## 5  Validation
* **Play-Mode Test** scene logs distinct asteroid counts per arena.  
* **Head-less Perf**: `-batchmode -nographics` runs 4-arenas × 30 s < 200 ms total.
* **ML-Agents Smoke**: `mlagents-learn` with `--num-envs 1` loads arena & steps without null-refs.

---

## 6  Risks & Mitigations
* **Shared singletons** (e.g., `GameManager`) might leak across arenas – audit & convert to arena-local or remove for training scene.
* **Physics layer collisions** between arenas – ensure each arena is spaced ≥ spacing so explosion radii never overlap.

---

## 7  Done-When
* PR merged, CI perf test passes, and RL pipeline can sample from ≥ 4 parallel arenas on dev machine. 

## 8  Making an "Endless" World RL-Friendly

| Design goal | Practical tweak | Why it helps training |
| --- | --- | --- |
| **Keep episodes short and data-dense** | Divide space into **finite "sectors"** (e.g., 3 km × 3 km boxes). Terminate an episode when the player leaves the box, wins, or dies. Spawn the next episode in a freshly randomized sector. | PPO/SAC learn faster when reward signals arrive every few thousand steps instead of after an unbounded wander. |
| **Parallelize like normal arenas** | Instantiate *N* independent sectors **per Unity worker** (ML-Agents' `--num-envs` flag). Each sector runs its own asteroid seed and agent pair, sharing the same physics scene to save CPU. | You still get dozens of concurrent rollouts even though the "real" game feels infinite. |
| **Expose only *local* observations** | Feed the policy relative vectors (player → enemy, nearest asteroid bearing, local density heat-map) and ignore absolute world coordinates. | The agent's policy stays invariant no matter which sector it spawns in. |
| **Procedural variety without overfitting** | On reset, randomize: asteroid count, average size, initial player/enemy separation, global drift velocity. Use ML-Agents Environment Parameters so the trainer sees new combos every episode. | "Domain randomization" teaches robustness you'll need for the live endless mode. |
| **Prevent boundary gaming** | Fade in a **soft perimeter cost**: a radial −0.05 reward per step once the agent is within, say, 200 m of the sector wall, plus a hard episode end if it crosses. | The policy learns to fight near center space without incentives to kite at edges. |
| **Curriculum for difficulty scaling** | Start with 1 km boxes, few asteroids. Gradually enlarge boxes and density as mean reward exceeds threshold. | Early on, the agent experiences dense learning signals; later it practices longer chases. |
| **Fast resets** | Pool asteroids and ship prefabs. On episode end, reposition and re-seed instead of destroying/instantiating objects. | Keeps step time low, which directly boosts sample throughput. |

### 8.1  Step-by-Step Integration

1. **Sector manager**

   ```csharp
   public class SectorSpawner : MonoBehaviour {
       public float halfSize = 1500f;   // 3 km box
       public void ResetSector(int seed) { /* place player, enemy, asteroids */ }
   }
   ```

   Attach one to each training environment GameObject (e.g., under `RLTrainingArena`).

2. **Episode boundaries** – in the `Agent` subclass call `EndEpisode()` when:
   * `Vector3.Distance(transform.position, sectorCenter) > halfSize`
   * Enemy destroyed / player destroyed
   * Max step count reached (e.g., 5 000 physics frames)

3. **Observation code** – only local features:

   ```csharp
   public override void CollectObservations(VectorSensor s) {
       s.AddObservation(toEnemy.normalized);
       s.AddObservation(toEnemy.magnitude / halfSize); // 0-1
       s.AddObservation(relVelocity / maxSpeed);
       s.AddObservation(LocalAsteroidDensity());       // e.g., 8-bin histogram
   }
   ```

4. **Parallel rollout settings**

   ```bash
   mlagents-learn config.yaml --run-id=dogfight --env=Build.exe --num-envs=16
   ```

5. **Play-mode hand-off** – disable sector walls & soft penalty in the live game. The trained policy simply never hits the boundary.

### 8.2  Impact on Existing Change List

* **C-1** gains an extra requirement: `SectorSpawner` must call `AsteroidFieldManager.SetAnchor()` with its player transform.
* **C-3** arena prefab becomes the template for a sector – we'll duplicate it via `SectorSpawner` instead of `ArenaSpawner`.
* **C-4** reset script must now handle sector wall penalties and curriculum parameters.

_No additional timeline impact — tasks slot into Day 2 after multi-arena support is proven._ 