# RLCommanderAgent – Implementation Plan

> High-level reinforcement-learning agent for piloting a player ship, avoiding asteroids, and defeating enemy ships.

---

## 1. Agent Setup
1. Create a new C# script named `RLCommanderAgent` that inherits from `Unity.MLAgents.Agent`.
2. Add the script **and** a `Decision Requester` component to the ship prefab intended for RL control.
3. Assign a `Behavior Parameters` component (details below).

## 2. Action Space
*Mixed (continuous + discrete)*

| Branch | Type | Size | Index → Meaning |
|-------:|------|-----:|-----------------|
| 0      | Continuous | **3** | 0 Thrust (−1 rev … +1 fwd)<br>1 Strafe (−1 left … +1 right)<br>2 Yaw    (−1 left … +1 right) |
| 1      | Discrete   | **2** | `Fire1` (0 = idle, 1 = shoot) |
| 2      | Discrete   | **2** | `Fire2` (0 = idle, 1 = shoot) |

### `OnActionReceived(ActionBuffers actions)` (pseudo-code)
```csharp
float thrust  = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
float strafe  = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
float yaw     = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

shipCommander.SetThrust(thrust);
shipCommander.SetStrafe(strafe);
shipCommander.SetYaw(yaw);

bool fire1 = actions.DiscreteActions[0] == 1;
bool fire2 = actions.DiscreteActions[1] == 1;
if (fire1) shipCommander.FirePrimary();
if (fire2) shipCommander.FireSecondary();
```

## 3. Observation Space
### 3.1 Vector Observations (`CollectObservations`)
| Feature | Notes | Normalization |
|---------|-------|---------------|
| Forward speed | Rigidbody dot (transform.forward, velocity) | ÷ `maxSpeed` |
| Lateral speed | Rigidbody dot (transform.right, velocity)   | ÷ `maxSpeed` |
| Yaw rate      | Ship's angular y velocity                   | ÷ `maxYawRate` |
| Shield / health | Current / max                              | 0 … 1 |
| Dir-to-closest enemy (x,y,z) | Local space vector (normalized) | Already −1…1 |
| Dir-to-closest asteroid (x,y,z) | Local space vector (normalized) | Already −1…1 |
| Dist-to-enemy | ÷ `sensingRange` | 0 … 1 |
| Dist-to-asteroid | ÷ `sensingRange` | 0 … 1 |

Total vector size = **8**.

### 3.2 Raycast Perception
Attach **one or more** `RayPerceptionSensorComponent3D` objects (added in the Unity Editor).
* Detectable Tags: `"Asteroid"`, `"Enemy"`, `"Projectile"`, `"Boundary"`
* Rays Per Direction: 5
* Max Ray Degrees: 75
* Ray Length: 60 m (≈ weapon range)
* Sphere Radius: 0 or 0.2
* Observation Stacks: 2–4 (start with 2)
* Add a second sensor with ± 0.5 m vertical offset to capture vertical clearance.

> Estimated ray observation size = `Stacks × (1 + 2·RPD) × (Tags + 2)` = `2 × 11 × 6 = 132` values.

## 4. Reward Scheme (baseline)
* `+1.0`   for damaging an enemy
* `+5.0`   for destroying an enemy
* `-2.0`   for taking damage
* `-5.0`   for colliding with an asteroid or being destroyed
* `-0.001` per physics step (time penalty)
* `+0.2`   per second survived (optional shaping)

## 5. Episode Termination
* Ship destroyed → end episode (failure)
* All enemies eliminated → end episode (success)
* Max episode length (e.g. 20 s) reached → time-out

## 6. Key Agent Methods
```csharp
public override void OnEpisodeBegin() { /* reset ship, asteroids, enemies */ }
public override void CollectObservations(VectorSensor sensor) { /* section 3.1 */ }
public override void OnActionReceived(ActionBuffers act) { /* section 2 */ }
public override void Heuristic(in ActionBuffers actOut) { /* keyboard debug */ }
```

### Heuristic mapping example
```csharp
var c = actOut.ContinuousActions;
c[0] = Input.GetAxis("Vertical");   // Thrust: W/S
c[1] = Input.GetAxis("Horizontal"); // Strafe: A/D
c[2] = Input.GetAxis("Yaw");        // Yaw: Q/E or mouse X

var d = actOut.DiscreteActions;
d[0] = Input.GetButton("Fire1") ? 1 : 0;
d[1] = Input.GetButton("Fire2") ? 1 : 0;
```

## 7. Editor Configuration Steps
1. Add/verify `Behavior Parameters`:
   * Continuous Actions = 3
   * Discrete Branches = 2 with sizes 2 & 2
2. Tick **Use Child Sensors** or attach sensors manually.
3. Set Decision Requester:
   * Decision Interval = 3 – 5 physics steps
   * Take Actions Between Decisions = ✔️

## 8. Training Tips
* **Curriculum learning**: start in an open arena with sparse asteroids & a single enemy; incrementally raise density & enemy AI difficulty.
* Disable VFX/audio during training to maximise simulation speed.
* Consider reward shaping for near-miss avoidance or energy efficiency.

## 9. Future Improvements
* Add rear or short-range sensors for coverage gaps.
* Experiment with **continuous firing rates** (replace binary discrete).
* Swap to **all-continuous** actions once basic competency achieved.
* Try an RNN or observation stacking > 2 for smoother behaviour.

---

With this plan, the `RLCommanderAgent` should have sufficient control authority and environmental awareness to learn effective piloting, obstacle avoidance, and combat strategies. 