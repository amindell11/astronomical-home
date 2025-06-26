Hybrid Boundary Scheme (soft pull + distant hard wall)
========================================================
Goal
----
Keep dense training signal **and** a clean ±1 win/loss trigger while supporting the existing *arena_size* randomization (40–80 m).

Strategy
--------
1. Two radii per episode
   * `R_soft`  = 0.75 × `arena_size`   → start quadratic penalty.  
   * `R_hard` = 1.20 × `arena_size`   → instant –1 / +1 + `EndEpisode()`.

2. Per-step penalty                  
   ```csharp
   float frac = (r - R_soft) / (R_hard - R_soft);  // 0 → 1
   float coef = 0.002f;                            // tune 0.001–0.005
   AddReward(-coef * frac * frac);                 // quadratic pull
   ```
   Penalty is radius-normalized, so behaviour is identical whether the arena is 40 m or 80 m.

3. Hard wall (unchanged logic)
   ```csharp
   if (r >= R_hard)
   {
       AddReward(-1f);
       opponent.AddReward(+1f);  // maintain zero-sum
       EndEpisodeForBoth();
   }
   ```

Migration Plan (from 1 M-step hard-wall policy)
----------------------------------------------
1. **New run-id** to start a fresh league:
   ```bash
   mlagents-learn config/PilotAgent.yaml \
                 --run-id=OpenArena_v1 \
                 --initialize-from=HardWall_v3
   ```

2. **Warm-up curriculum** (optional)
   * 0–200 k steps: old wall at 1.10 × `arena_size`, *plus* soft pull.  
   * >200 k steps: remove the old wall; keep `R_hard` = 1.20 × `arena_size`.

3. **Trainer tweaks**
   * `learning_rate` → `2e-4` for first 100 k steps.  
   * `time_horizon` → `2048` (longer episodes).  
   * Clear or restart the self-play league if continuing an old run.

Metrics to watch
----------------
| Scalar                        | Healthy trend                                   |
|-------------------------------|-------------------------------------------------|
| `Stats/AverageRadiusFrac`     | Settles < 0.6                                   |
| `Self-Play/ELO`               | Dips ≤100 k → recovers & climbs                 |
| `Episode Length`              | Below `Max Step`; no upward drift               |
| `Losses/Value Loss`           | Spike at switch, then gradual decline           |

Why this works
--------------
* **Dense gradient** keeps learning fast.  
* **Zero-sum hard wall** preserves clean win/loss → ELO stable.  
* **Radius-normalized numbers** mean random arena sizes don’t break the shaping.  
* Requires minimal code change and no redesign of reward tables.