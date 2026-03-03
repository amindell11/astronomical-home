# Objective Tracker — Codebase Map & Implementation Reference

Quick lookup guide for existing patterns, file locations, and integration points.

---

## 1. STATE MACHINE PATTERNS (Template to Emulate)

### State Base Class
**Location:** `src/Asteroids3D/Assets/Scripts/AI/States/State.cs`

```csharp
public abstract partial class State {
    protected readonly Navigator navigator;
    protected readonly Gunner gunner;
    protected readonly UtilityTuning utilityTuning;
    
    public abstract StateType Type { get; }
    
    public virtual void Enter(Info ctx) { }
    public abstract void Tick(Info ctx, float deltaTime);
    public virtual void Exit() { }
    public abstract float ComputeUtility(Info ctx);
}
```

**Key Pattern Elements:**
- Constructor injection of dependencies
- Lightweight context struct (`Info`) passed to all methods
- Optional `ComputeUtility()` for priority-based selection (useful for objective feasibility)
- Clear lifecycle: Enter → Tick → Exit

### Concrete State Examples
**Location:** `src/Asteroids3D/Assets/Scripts/AI/States/`
- `Patrol.cs` — Waypoint-based movement
- `Attack.cs` — Combat engagement
- `Evade.cs` — Evasive maneuvers
- `Kite.cs` — Long-range combat strategy
- `Orbit.cs` — Orbital positioning

### State Machine Container
**Location:** `src/Asteroids3D/Assets/Scripts/AI/States/AIStateMachine.cs` (implied, not shown)
- Maintains current state
- Computes utilities & selects highest-scoring state
- Calls Enter/Tick/Exit on selected state
- Caches context struct to avoid per-frame allocations

---

## 2. GAME INITIALIZATION & INTEGRATION POINTS

### Main Game Initializer
**Location:** `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs`

**Key Methods:**
```csharp
public class GameInitiator : MonoBehaviour {
    public event Action<Ship, Camera> PresentationReady;
    
    // Initialization sequence
    private IEnumerator Initialize(GameConfig config) {
        yield return LoadWorldScene();
        InitializeWorld(gameConfig);
        InitializeAsteroidField(gameConfig);
        InitializeShips(gameConfig);
        InitializeCamera(gameConfig);
        PublishPresentationReady();
    }
    
    // Lifecycle
    public void Shutdown() { }
}
```

**Integration Point for ObjectiveTracker:**
1. Instantiate ObjectiveTracker in `Initialize()` method (after ships initialized)
2. Subscribe to `GameInitiator.PresentationReady` event
3. Start objective tick in `Update()` loop

### Game Configuration
**Location:** `src/Asteroids3D/Assets/Scripts/Game/GameConfig.cs`

```csharp
[CreateAssetMenu]
public class GameConfig : ScriptableObject {
    public Ship PlayerTemplate { get; }
    public Ship EnemyTemplate { get; }
    public UpdatingAsteroidField AsteroidAsteroidField { get; }
    // ... more config
}
```

**Extension for Objectives:**
Add to GameConfig:
```csharp
public ObjectiveParams ObjectiveParameters { get; }
public MissionDefinition[] Missions { get; }
```

### Game Plane (Reference Coordinate System)
**Location:** `src/Asteroids3D/Assets/Scripts/Game/GamePlane.cs`

Provides world-to-plane projection utilities. Use for objective markers/extraction points:
```csharp
public static Vector3 ProjectOntoPlane(Vector3 worldPos) { }
public static Vector3 PlanePointToWorld(Vector2 planePos) { }
```

---

## 3. EVENT-DRIVEN PATTERNS (Model to Follow)

### Event Test Cases
**Location:** `src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/EventDrivenRefactorEditModeTests.cs`

**Pattern to Follow:**
```csharp
public event Action<StateFrom, StateTo> OnStateChanged;

// Usage
public void TransitionState(StateType newState) {
    var previous = currentState;
    currentState = newState;
    OnStateChanged?.Invoke(previous, newState);
}

// Subscription
subscriber.OnStateChanged += (from, to) => HandleTransition(from, to);
```

### Lock State Events (Reference Implementation)
**Location:** `src/Asteroids3D/Assets/Scripts/Combat/Targeting/LockState.cs`

```csharp
public enum LockState { Idle, Locking, Locked, Cooldown }

public interface ILockStateSource {
    LockState State { get; }
    event Action<LockState, LockState> OnStateChanged;
}
```

**Key Concept:** UI components subscribe to state change events rather than polling.

### UI Event Subscription Pattern
**Relevant Classes:**
- `src/Asteroids3D/Assets/Scripts/UI/MissileAmmoUI.cs`
- `src/Asteroids3D/Assets/Scripts/UI/LaserHeatUI.cs`
- `src/Asteroids3D/Assets/Scripts/UI/Audio/UILockOnAudio.cs`

**Pattern:**
```csharp
public class ObjectiveHUD : MonoBehaviour {
    private ObjectiveTracker tracker;
    
    private void OnEnable() {
        if (tracker) {
            tracker.OnStateChanged += UpdateObjectiveDisplay;
            tracker.OnProgressChanged += UpdateProgressBar;
        }
    }
    
    private void OnDisable() {
        if (tracker) {
            tracker.OnStateChanged -= UpdateObjectiveDisplay;
            tracker.OnProgressChanged -= UpdateProgressBar;
        }
    }
}
```

---

## 4. SHIP & DAMAGE SYSTEMS (Context for Objectives)

### Ship Class
**Location:** `src/Asteroids3D/Assets/Scripts/Ships/Ship.cs`

**Key Properties/Events:**
```csharp
public class Ship : MonoBehaviour {
    // Health/damage system
    public ShipDamageHandler DamageHandler { get; }
    public float CurrentHealth { get; }
    
    // Combat systems
    public TargetingComputer Targeting { get; }
    public IShooter[] Shooters { get; }
    
    // Commander system
    public Commander Commander { get; }
    
    // Registry
    public IShipRegistry ShipRegistry { get; set; }
}
```

**Integration:** Objective tracker can query ship health, weapons state, and position via these properties.

### Damage & Destruction
**Location:** `src/Asteroids3D/Assets/Scripts/Combat/` (specific files TBD)

**Expected Event Pattern (planned):**
```csharp
public class ShipEvents {
    public event Action<Ship, float> OnDamageDealt;
    public event Action<Ship> OnDestroyed;
    public event Action<Ship, float> OnShieldChanged;
}
```

**Usage in ObjectiveTracker:**
```csharp
private void OnEnemyDestroyed(Ship enemy) {
    // Trigger objective completion or transition
}
```

---

## 5. RESPAWN & LIFECYCLE MANAGEMENT

### Respawn Runner
**Location:** `src/Asteroids3D/Assets/Scripts/Game/` (name: ShipRespawnRunner implied)

**Integration Point:**
```csharp
// In ObjectiveTracker
private ShipRespawnRunner respawnRunner;

private void OnRespawnComplete() {
    // Reset objective state or transition
}
```

### Ship Registry
**Location:** `src/Asteroids3D/Assets/Scripts/Ships/` (implied)

**Pattern:**
```csharp
public class ShipRegistry {
    public IList<Ship> ActiveShips { get; }
    public event Action<Ship> OnShipAdded;
    public event Action<Ship> OnShipRemoved;
}
```

**Usage:**
```csharp
ShipRegistry.OnShipRemoved += (ship) => {
    if (ship.isPlayerShip) {
        // Objective failed: player destroyed
    }
};
```

---

## 6. PERFORMANCE & TESTING PATTERNS

### Context Struct Pattern (Zero Allocations)
**Example from AI System:**
```csharp
public struct AIContext {
    public float shieldPct;
    public float relDistance;
    public float relSpeed;
    public bool lineOfSight;
    public bool incomingMissile;
    public int nearbyFriendCount;
}

// Passed as `in` parameter (stack, not heap)
public void Tick(in AIContext ctx, float deltaTime) { }
```

**Apply to ObjectiveTracker:**
```csharp
public struct ObjectiveContext {
    public ObjectiveType type;
    public float progressPct;
    public float timeElapsed;
    public Vector3 playerPos;
    public bool hasKey;
    public int enemyCount;
}
```

### Performance Constraints
**Reference:** `doc/Feature_Plans/AI_Performance_Optimization.md`

**Rules:**
- Zero per-frame managed allocations
- Event subscription overhead < 1 ms combined
- All debug code wrapped in `#if UNITY_EDITOR`
- Deterministic code paths (no reflection at runtime)

### Editor-Gating Convention
```csharp
#if UNITY_EDITOR
    Debug.Log("Objective state: " + CurrentState);
    Gizmos.DrawSphere(transform.position, 5f);
#endif
```

---

## 7. TESTING INFRASTRUCTURE

### Test Location Structure
```
Assets/
  Tests/
    EditMode/
      ObjectiveTrackerEditModeTests.cs      ← Unit tests (state transitions)
    PlayMode/
      ObjectiveTrackerPlayModeTests.cs      ← Integration tests (scene setup)
      ObjectiveHUDPlayModeTests.cs          ← UI subscription tests
```

### EditMode Test Pattern (Fast, Isolated)
```csharp
[Test]
public void ObjectiveTracker_TransitionsFromExploreToKeyAcquired() {
    var tracker = new ObjectiveTracker();
    tracker.Initialize(mockConfig);
    
    var transitions = new List<(ObjectiveState from, ObjectiveState to)>();
    tracker.OnStateChanged += (f, t) => transitions.Add((f, t));
    
    tracker.Tick(10f); // Simulate 10 seconds of exploration
    
    Assert.Contains((ObjectiveState.Explore, ObjectiveState.KeyAcquired), transitions);
}
```

### PlayMode Test Pattern (Scene Integration)
```csharp
[UnityTest]
public IEnumerator ObjectiveHUD_UpdatesOnStateChange() {
    // Setup scene with GameInitiator + ObjectiveTracker + HUD
    var tracker = FindObjectOfType<ObjectiveTracker>();
    var hud = FindObjectOfType<ObjectiveHUD>();
    
    tracker.TransitionState(ObjectiveState.KeyAcquired);
    yield return new WaitForSeconds(0.1f);
    
    Assert.AreEqual("Key Acquired", hud.ObjectiveText.text);
}
```

---

## 8. CONFIGURATION & TUNING

### ScriptableObject Pattern
**Reference:** `src/Asteroids3D/Assets/Scripts/Game/GameConfig.cs`

**Create for Objectives:**
```csharp
[CreateAssetMenu(fileName = "ObjectiveParams", menuName = "Game/Objective Parameters")]
public class ObjectiveParams : ScriptableObject {
    [SerializeField] private float exploreCompletionThreshold = 0.8f;
    [SerializeField] private float extractionTimeLimit = 300f;
    [SerializeField] private float keySpawnRadius = 50f;
    [SerializeField] private float failureTimeLimit = 600f;
    
    public float ExploreThreshold => exploreCompletionThreshold;
    public float ExtractTimeLimit => extractionTimeLimit;
    // ... getters for all tunables
}
```

### Behavior Upgrades Tuning Pattern
**Reference:** `doc/Feature_Plans/Behavior_Upgrades.md`

Similar approach for objective states:
```csharp
public class ObjectiveStateParams : ScriptableObject {
    // Parameters specific to each state type
}
```

---

## 9. ML-AGENTS / RL INTEGRATION POINTS

### RLArbiter (Stub, Already Created)
**Location:** `src/Asteroids3D/Assets/Scripts/` (implied)

**Should Subscribe to:**
```csharp
public class RLArbiter : MonoBehaviour {
    private ObjectiveTracker objectiveTracker;
    
    private void OnObjectiveStateChanged(ObjectiveState from, ObjectiveState to) {
        // Calculate reward bonus
        // Update observation vector
    }
}
```

### Observation Vector Extension
**Add to RLArbiter:**
```csharp
public float[] GetObservationVector() {
    // Existing 32 observations
    // + objective type (discrete)
    // + objective progress (0–1)
    // + time in objective
    // + active objective count
}
```

### Reward Shaping
**Pattern (from Behavior_Upgrades.md):**
```csharp
private float ComputeObjectiveReward() {
    float baseReward = 0.01f; // Per frame
    float progressBonus = objectiveTracker.CurrentProgress * 0.05f;
    float completionBonus = objectiveTracker.IsComplete ? 0.5f : 0f;
    
    return baseReward + progressBonus + completionBonus;
}
```

---

## 10. QUICK CHECKLIST FOR IMPLEMENTATION

### Files to Create
- [ ] `Assets/Scripts/Game/Objectives/ObjectiveState.cs` (abstract base)
- [ ] `Assets/Scripts/Game/Objectives/ObjectiveTracker.cs` (main component)
- [ ] `Assets/Scripts/Game/Objectives/States/ExploreState.cs`
- [ ] `Assets/Scripts/Game/Objectives/States/KeyAcquiredState.cs`
- [ ] `Assets/Scripts/Game/Objectives/States/ExtractionChallengeState.cs`
- [ ] `Assets/Scripts/Game/Objectives/States/ExtractedState.cs`
- [ ] `Assets/Scripts/Game/Objectives/States/FailedState.cs`
- [ ] `Assets/Scripts/Game/Objectives/ObjectiveParams.cs` (ScriptableObject)
- [ ] `Assets/Scripts/UI/ObjectiveHUD.cs` (UI component)
- [ ] `Assets/Tests/EditMode/ObjectiveTrackerEditModeTests.cs`
- [ ] `Assets/Tests/PlayMode/ObjectiveTrackerPlayModeTests.cs`

### Files to Modify
- [ ] `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs` (hook tracker initialization)
- [ ] `src/Asteroids3D/Assets/Scripts/Game/GameConfig.cs` (add ObjectiveParams reference)
- [ ] `src/Asteroids3D/Assets/Scripts/Ships/Ship.cs` (event hooks if needed)

### Configuration Assets to Create
- [ ] `Assets/ScriptableObjects/ObjectiveParams_MVP.asset`
- [ ] `Assets/ScriptableObjects/GameConfig_WithObjectives.asset`

---

## 11. REFERENCE DOCUMENTS

| Document | Purpose |
|----------|---------|
| `doc/Feature_Plans/AI_StateSystem_Refactor.md` | State machine pattern (template to follow) |
| `doc/Feature_Plans/AI_StateSystem_Refactor_Summary.md` | Implementation record (context struct usage) |
| `OBSIDIAN_SCOUT_REPORT.md` | Event-driven architecture guide |
| `doc/Feature_Plans/RL_Implementation_Plan.md` § 9 | RL integration requirements |
| `doc/Feature_Plans/Testing_Plan.md` | Test patterns & modalities |
| `doc/Feature_Plans/Behavior_Upgrades.md` | Tuning & reward shaping patterns |
| `.ralph/objective-tracker-state-machine.md` | Original task plan (MVP scope) |

---

**End of Codebase Map**  
Use this as a quick reference while implementing. Refer to OBJECTIVE_TRACKER_RESEARCH.md for detailed architecture decisions.
