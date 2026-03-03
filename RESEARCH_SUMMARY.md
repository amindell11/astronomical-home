# Objective Tracker State Machine — Research Summary

**Research Date:** March 3, 2026  
**Scope:** Main objective tracker feature covering missions, objectives, states, extensibility, and integration  
**Status:** Ready for design review and implementation planning

---

## EXECUTIVE SUMMARY

The objective tracker is a **core progression system** that will manage missions, objectives, and state transitions in *Dogfight AIsteroids*. The research has identified:

✅ **Battle-tested patterns** to follow (AI state machine, event-driven architecture)  
✅ **Clean integration points** in existing codebase (GameInitiator, GameConfig, ShipRegistry)  
✅ **Performance constraints** that must be respected (zero per-frame allocations)  
⚠️ **6 design decisions** that must be locked before implementation begins

---

## 1. WHAT OBJECTIVES/MISSIONS SYSTEM EXISTS OR IS PLANNED

### Current State: No Existing System
- The codebase has **no mission or objective tracker** today
- Game loop is single-encounter MVP: spawn player, spawn enemy, dogfight until someone dies
- No explicit win conditions, progression, or multi-encounter campaigns
- ShipRegistry tracks active ships but doesn't correlate to objectives

### Planned Direction (from Proposal & Roadmap):
- **Multi-sector campaign** structure implied by project design
- **Missions** bundle multiple encounters with linked objectives
- **Sectors** represent spatial regions with distinct densities, enemies, and loot
- **Progression** unlocks weapons, upgrades, or difficulty tiers across sectors
- **Reinforcement learning** integration requires objective observation & reward feedback

### MVP Scope (from `.ralph/objective-tracker-state-machine.md`):
```
Single-sector MVP loop with 4-5 states:
Explore → KeyAcquired → ExtractionChallenge → Extracted | Failed
```

**Source Paths:**
- `.ralph/objective-tracker-state-machine.md` — MVP scope & constraints
- `doc/Feature_Plans/RL_Implementation_Plan.md` § 9 — RL pipeline requirements
- `doc/Proposal.md` — Project vision & campaign structure

---

## 2. WHAT STATES THE OBJECTIVE TRACKER SHOULD SUPPORT

### MVP States (Defined & Ready to Implement)

**1. Explore**
- Player scouts the sector, discovers threats and resources
- Progress metric: sector coverage percentage (inferred from player movement)
- Exit condition: coverage ≥ threshold (tuneable via ObjectiveParams)
- Next state: KeyAcquired (automatically when threshold met)

**2. KeyAcquired**
- Mission-critical artifact/key spawns and waits for pickup
- Triggers when Explore completes
- Progress metric: has player collected the key?
- Exit condition: player collects key
- Next state: ExtractionChallenge

**3. ExtractionChallenge**
- Player must survive enemy pursuit while escaping sector
- Unlocked only after KeyAcquired state
- Progress metric: distance traveled toward extraction zone
- Exit condition: player reaches extraction point OR player dies OR time expires
- Next states: Extracted (success) or Failed

**4. Extracted** (Terminal)
- Player successfully completed objective
- Triggers wingman callout (stub)
- Audio plays mission-complete SFX
- RL reward bonus applied
- Next: Game loop ends or next mission begins

**5. Failed** (Terminal)
- Objective did not complete
- Triggers when: player destroyed, time expires, enemy escapes, extraction blocked
- RL penalty applied
- Can retry or return to menu (design decision needed)

### Future States (Beyond MVP)
- **Defend:** Protect objective zone from enemies
- **Retrieve:** Collect multiple items scattered across sector
- **Survive:** Endure X time or reach score threshold
- **Escort:** Protect NPC or asset through sector
- **Custom:** Dynamic objectives loaded from mission definition

**Source Paths:**
- `OBJECTIVE_TRACKER_RESEARCH.md` § 4 — Mission/objective structure gap analysis
- `OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md` § Phase 1 — Detailed state implementations

---

## 3. HOW MISSIONS AND OBJECTIVES RELATE TO EACH OTHER

### Hierarchy Model (Inferred from Design Context)

```
Campaign (multi-sector progression)
  ↓
Mission (bundle of objectives, tied to one sector)
  ├─ Objective 1 (Explore)
  ├─ Objective 2 (KeyAcquired)
  └─ Objective 3 (ExtractionChallenge)
       ↓
    Sub-objectives (future: nested goals within single objective)
```

### Relationship Semantics

**1. Missions Contain Objectives**
- One mission per playthrough (MVP scope)
- Mission = ordered sequence of states + completion criteria
- Multiple encounters per mission possible (respawn/retry cycles)

**2. Objectives Are State-Driven**
- Each objective is a state in the FSM
- Objectives transition based on game conditions (player position, item pickups, time)
- No explicit "objective list"; instead, current state IS the active objective

**3. Sequential vs. Parallel** (Design Decision TBD)
- MVP assumes **sequential**: Explore → KeyAcquired → ExtractionChallenge
- Future may support **parallel**: multiple active objectives (e.g., "Explore AND Survive 10 mins")
- No nesting in MVP; flat linear progression

**4. Persistence & Retry** (Design Decision TBD)
- Does mission progress persist across scenes?
- Can player retry failed objective without full reset?
- Connection to respawn system (ShipRespawnRunner)?

### Integration with Game Loop

```
GameInitiator.PresentationReady
  ↓
ObjectiveTracker.Initialize(mission config)
  ↓
Each frame: ObjectiveTracker.Tick(deltaTime)
  ├─ Updates ObjectiveContext (playerPos, hasKey, enemyCount, etc.)
  ├─ Calls currentState.Tick(context, deltaTime)
  ├─ Evaluates state transition condition
  └─ Publishes ObjectiveStateChanged event
       ↓
UI/Audio/RL subscribers handle updates
```

**Source Paths:**
- `OBJECTIVE_TRACKER_RESEARCH.md` § 1–2 — Game loop & state machine patterns
- `OBJECTIVE_TRACKER_CODEBASE_MAP.md` § 2 — GameInitiator integration points
- `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs` — Actual init flow

---

## 4. WHAT EXTENSIBILITY AND INTERFACE REQUIREMENTS EXIST

### Core Interfaces & Contracts

**A. ObjectiveState Base Class** (Public Contract)
```csharp
public abstract class ObjectiveState {
    public abstract ObjectiveType StateType { get; }
    public virtual void Enter(in ObjectiveContext ctx) { }
    public abstract void Tick(in ObjectiveContext ctx, float deltaTime);
    public virtual void Exit() { }
    public abstract ObjectiveState GetNextState(in ObjectiveContext ctx);
    public abstract float ComputeUtility(in ObjectiveContext ctx); // For priority-based selection
}
```

**Extensibility Points:**
- New states inherit from ObjectiveState
- No reflection; compile-time polymorphism only
- Context struct can be extended without breaking existing states
- Utility scoring enables priority-based objective selection (future)

**B. ObjectiveTracker Events** (Public Contract)
```csharp
public class ObjectiveTracker : MonoBehaviour {
    public event Action<ObjectiveState, ObjectiveState> OnStateChanged;
    public event Action<float> OnProgressChanged;        // progress: 0–1
    public event Action OnObjectiveCompleted;
    public event Action OnObjectiveFailed;
    
    // Read-only properties
    public ObjectiveState CurrentState { get; }
    public float CurrentProgress { get; }
    public float TimeInCurrentState { get; }
}
```

**Extensibility Points:**
- Subscribers (UI, Audio, RL) can be added without modifying tracker
- Event parameters allow loose coupling
- Re-subscription pattern allows respawn-safe subscriptions

**C. ObjectiveContext Struct** (Zero-Alloc Contract)
```csharp
public struct ObjectiveContext {
    public ObjectiveType type;
    public float progressPct;
    public float timeElapsed;
    public Vector3 playerPos;
    public Vector3 extractionPoint;
    public bool hasKey;
    public int enemyCount;
    public float asteroidDensity;
    // Extensible: add fields without breaking existing states
}
```

**Extensibility Points:**
- Add new fields as needed (no breaking changes if only read by new states)
- Struct is computed once per frame (no per-state allocation)
- Can be serialized for RL observation vector

**D. ObjectiveParams ScriptableObject** (Tuning Contract)
```csharp
[CreateAssetMenu]
public class ObjectiveParams : ScriptableObject {
    [SerializeField] public float ExploreThreshold = 0.8f;
    [SerializeField] public float ExtractTimeLimit = 300f;
    [SerializeField] public float KeySpawnRadius = 50f;
    [SerializeField] public float FailureTimeLimit = 600f;
    // ... more tunables per state
}
```

**Extensibility Points:**
- New states can have their own param subclasses (ObjectiveStateParams)
- No hardcoded values; all tuning via assets
- Enables difficulty scaling & playtesting variations

### Required Subscribers (Pre-defined Integration Points)

| Subscriber | Purpose | Status |
|-----------|---------|--------|
| **ObjectiveHUD** | Display current objective, progress bar | To implement |
| **ObjectiveAudio** | Play SFX on state changes, wingman callouts | To implement |
| **Wingman System** | Narrate objective transitions (stub) | To implement (stub) |
| **RLArbiter** | Feed observation vector, calculate reward | To implement |
| **GameInitiator** | Initialize tracker, handle shutdown | To modify |
| **ShipRegistry** | Detect player destruction (failure trigger) | Existing, may hook |

**Source Paths:**
- `OBJECTIVE_TRACKER_RESEARCH.md` § 5–7 — Architecture & patterns
- `OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md` § 1–5 — Complete interfaces with code
- `src/Asteroids3D/Assets/Scripts/AI/States/State.cs` — State pattern template (emulate this)

---

## 5. ANY EXISTING TRACKER OR QUEST SYSTEM PATTERNS

### ✅ Existing Patterns to Emulate

**A. AI State Machine** (Battle-Tested, Jan 2025)
- **Location:** `src/Asteroids3D/Assets/Scripts/AI/States/State.cs`
- **Pattern:** Abstract base with Enter/Tick/Exit lifecycle
- **Context:** Lightweight struct passed `in` parameter (zero-copy)
- **Utility Scoring:** ComputeUtility() for priority-based selection
- **Performance:** Zero allocations, deterministic code paths
- **Testing:** Unit-testable without scene context

**Why This Pattern Works for Objectives:**
- Lifecycle (Enter/Tick/Exit) maps naturally to objective activation/progress/completion
- Context struct enables zero-alloc state transitions
- Utility scoring can drive multi-objective prioritization (future)
- Already proven in production AI system

**B. Event-Driven Architecture** (In Progress, Blocking RL Pipeline)
- **Location:** `src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/EventDrivenRefactorEditModeTests.cs`
- **Pattern:** Event publishers with `Action<StateFrom, StateTo>` delegates
- **UI Integration:** Components subscribe in OnEnable/unsubscribe in OnDisable
- **Lifecycle:** Re-subscription handles respawn cycles
- **Examples:** LockController.OnStateChanged, Heat.OnHeatChanged

**Why This Pattern Works for Objectives:**
- UI can subscribe to ObjectiveStateChanged without tight coupling
- Audio system can react asynchronously to completion/failure
- RL pipeline can consume observation/reward without polling
- Respawn-safe (components re-subscribe after reset)

**C. Configuration via ScriptableObject** (Standard Practice)
- **Location:** `src/Asteroids3D/Assets/Scripts/Game/GameConfig.cs`
- **Pattern:** Tunable assets, no hardcoded values
- **Validation:** Properties expose safe getters
- **Integration:** Passed to components at initialization

**Why This Pattern Works for Objectives:**
- All state thresholds tuneable (ExploreThreshold, TimeLimit, etc.)
- No code changes needed for difficulty scaling or playtesting
- Can create variants for different mission types

**D. Dependency Injection** (Standard Practice)
- **Pattern:** Constructor or Initialize() method injection
- **Benefits:** Loose coupling, testability without scene context
- **Examples:** Navigator, Gunner injected into State; GameConfig passed to GameInitiator

**Why This Pattern Works for Objectives:**
- ObjectiveTracker can inject ShipRegistry, GameConfig, EventPublisher
- States can be instantiated and tested in isolation
- No global state; arena-scoped trackers for multi-arena training

### ❌ No Existing Quest/Mission/Objective Tracker
- Codebase has no mission system yet (confirmed gap)
- No tracker UI component exists
- No multi-encounter progression logic
- No reward-shaping integration with RL

**Source Paths:**
- `doc/Feature_Plans/AI_StateSystem_Refactor.md` — State machine spec & context pattern
- `OBSIDIAN_SCOUT_REPORT.md` — Event-driven architecture comprehensive guide
- `src/Asteroids3D/Assets/Scripts/Combat/Targeting/LockState.cs` — Event pattern reference
- `src/Asteroids3D/Assets/Scripts/UI/MissileAmmoUI.cs` — UI subscription pattern reference

---

## 6. INTEGRATION POINTS WITH THE GAME LOOP OR OTHER SYSTEMS

### A. GameInitiator (Primary Orchestrator)

**Current Flow:**
```
GameInitiator.Initialize(GameConfig)
  ├─ LoadWorldScene()
  ├─ InitializeWorld()
  ├─ InitializeAsteroidField()
  ├─ InitializeShips()
  ├─ InitializeCamera()
  └─ PublishPresentationReady()
```

**Integration Point for ObjectiveTracker:**
1. Create ObjectiveTracker GameObject in Initialize() method
2. Call ObjectiveTracker.Initialize(config.ObjectiveParameters)
3. Subscribe to PresentationReady event to start objective state machine
4. Call ObjectiveTracker.Shutdown() in GameInitiator.Shutdown()

**Code Pattern:**
```csharp
private ObjectiveTracker objectiveTracker;

private IEnumerator Initialize(GameConfig config) {
    // ... existing initialization
    var trackerGO = new GameObject("ObjectiveTracker");
    objectiveTracker = trackerGO.AddComponent<ObjectiveTracker>();
    objectiveTracker.Initialize(config.ObjectiveParameters, ShipRegistry);
    
    PresentationReady?.Invoke(playerShip, mainCamera);
}

private void Shutdown() {
    if (objectiveTracker) Destroy(objectiveTracker.gameObject);
    // ... existing cleanup
}
```

**Source:** `OBJECTIVE_TRACKER_CODEBASE_MAP.md` § 2, `OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md` § Phase 2

---

### B. ShipRegistry (Ship Lifecycle Events)

**Integration for Failure Detection:**
```csharp
ShipRegistry.OnShipRemoved += (ship) => {
    if (ship.isPlayerShip) {
        objectiveTracker.TriggerFailure("Player destroyed");
    }
};
```

**Benefits:**
- Automatically detects player death (failure trigger)
- No polling required
- Respawn-safe (ShipRegistry re-adds ship after respawn)

**Source:** `OBJECTIVE_TRACKER_RESEARCH.md` § 1, `OBJECTIVE_TRACKER_CODEBASE_MAP.md` § 5

---

### C. Game Loop (Frame-by-Frame Updates)

**Objective Tracker Tick Integration:**
```csharp
// In GameInitiator or main Update() loop
private void Update() {
    if (objectiveTracker) {
        objectiveTracker.Tick(Time.deltaTime);
    }
}
```

**What Happens Each Frame:**
1. Objective context updated (player position, hasKey, enemyCount, etc.)
2. Current state.Tick(context, deltaTime) evaluates progress
3. State transition logic checks if GetNextState() returns new state
4. OnStateChanged event published if transition occurs
5. RL observation vector updated (for ML-Agents integration)

**Source:** `OBJECTIVE_TRACKER_CODEBASE_MAP.md` § 2, § 6

---

### D. Respawn System (ShipRespawnRunner)

**Integration Points:**
- Objective can be reset when player respawns (design decision TBD)
- Or objective continues (player must retrieve key again)
- ShipRespawnRunner triggers re-subscription of objective HUD

**Design Question (Phase 0):**
- Does respawn reset objective state to Explore?
- Or does it preserve progress (retrying same state)?

**Source:** `OBJECTIVE_TRACKER_RESEARCH.md` § 4 (open questions), `OBJECTIVE_TRACKER_CODEBASE_MAP.md` § 5

---

### E. ML-Agents / RL Pipeline (Blocking Dependency)

**Current Status:** Planned but blocked on ShipEvents/GameStateEventsFacade

**Required Integration:**
1. ObjectiveTracker publishes objective state changes
2. RLArbiter.cs subscribes to ObjectiveTracker events
3. Observation vector extended with objective data:
   - Objective type (discrete)
   - Objective progress (float 0–1)
   - Time in objective (float, normalized)
   - Active objective count (int)

**Reward Shaping Formula:**
```csharp
float baseReward = 0.01f * objectiveTracker.CurrentProgress;
float completionBonus = objectiveTracker.IsComplete ? 0.5f : 0f;
float failurePenalty = objectiveTracker.IsFailed ? -0.5f : 0f;
return baseReward + completionBonus + failurePenalty;
```

**Constraints:**
- Zero global state (arena-scoped trackers for multi-arena training)
- Deterministic observation vector (no random variation)
- Headless support (no Camera.main dependency)

**Source:** `OBJECTIVE_TRACKER_RESEARCH.md` § 8, `OBJECTIVE_TRACKER_CODEBASE_MAP.md` § 9, `doc/Feature_Plans/RL_Implementation_Plan.md` § 9

---

### F. UI Systems (HUD, Objective Log)

**Event Flow:**
```
ObjectiveTracker.OnStateChanged(oldState, newState)
  ↓
ObjectiveHUD.UpdateObjectiveDisplay(newState)
  ├─ Update text label ("Exploring Sector...")
  ├─ Update progress bar (0–1)
  └─ Maybe trigger animation or icon change

ObjectiveTracker.OnProgressChanged(progress)
  ↓
ObjectiveHUD.UpdateProgressBar(progress)
```

**Subscription Pattern (Respawn-Safe):**
```csharp
public class ObjectiveHUD : MonoBehaviour {
    private void OnEnable() {
        if (tracker) {
            tracker.OnStateChanged += HandleStateChange;
            tracker.OnProgressChanged += HandleProgressChange;
        }
    }
    
    private void OnDisable() {
        if (tracker) {
            tracker.OnStateChanged -= HandleStateChange;
            tracker.OnProgressChanged -= HandleProgressChange;
        }
    }
}
```

**Source:** `OBJECTIVE_TRACKER_CODEBASE_MAP.md` § 3, `OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md` § Phase 3

---

### G. Audio System (SFX + Wingman Callouts)

**Event Integration:**
```csharp
public class ObjectiveAudio : MonoBehaviour {
    private ObjectiveTracker tracker;
    
    private void OnEnable() {
        tracker.OnStateChanged += PlayStateTransitionAudio;
        tracker.OnObjectiveCompleted += PlayCompletionSFX;
        tracker.OnObjectiveFailed += PlayFailureSFX;
    }
}
```

**Audio Triggers:**
- Explore state: ambient exploration music cue
- KeyAcquired state: key pickup SFX + wingman "Key acquired" callout
- ExtractionChallenge state: high-tension combat music
- Extracted state: mission-complete fanfare + wingman celebration
- Failed state: mission-failed SFX + wingman regret/apology

**Wingman Stub (Future):**
- Currently: text-based placeholder
- Future: voice lines, narrative hooks, dynamic responses

**Source:** `OBJECTIVE_TRACKER_RESEARCH.md` § 7, `OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md` § Phase 4

---

### H. Extraction Mechanics (Design Decision TBD)

**Unresolved Questions:**
- How does extraction trigger? (manual escape pod, auto-jump gate, reach edge of map?)
- Is extraction point fixed or random each run?
- Can extraction be blocked (by enemies, asteroids)?
- What's the win condition? (reach point, survive X time, escape with key?)

**Implications:**
- ExtractionChallengeState.Tick() must check extraction condition
- ObjectiveContext needs extractionPoint location
- Game loop must detect when player reaches extraction

**Source:** `OBJECTIVE_TRACKER_RESEARCH.md` § 4 (design decisions)

---

## IMPORTANT CONSTRAINTS

### **A. Architectural Constraints**

1. **Follow AI State Machine Pattern**
   - Emulate `src/Asteroids3D/Assets/Scripts/AI/States/State.cs`
   - Enter/Tick/Exit lifecycle required
   - Context struct for all state data (no per-state fields)
   - No MonoBehaviour dependencies in state classes themselves

2. **Event-Driven, Not Polling**
   - Objective tracker publishes events
   - UI/Audio/RL subscribe, don't ask
   - No GetObjectiveState() queries from UI

3. **Zero Global State**
   - Arena-scoped trackers only
   - Multi-arena training requires separate tracker per arena
   - No singleton ObjectiveTracker

4. **Non-Destructive**
   - Existing single-encounter dogfight loop must remain playable
   - Objective system optional (can disable via GameConfig)
   - No breaking changes to Ship, GameInitiator, etc.

5. **Respawn-Safe**
   - UI components re-subscribe in OnEnable/unsubscribe in OnDisable
   - Wingman persistence = stub/placeholder for now
   - Event handlers must not assume permanent subscriptions

### **B. Performance Constraints**

1. **Zero Per-Frame Allocations**
   - ObjectiveContext is struct, not class
   - No List.Add() or string concatenation in Tick()
   - All state transitions must be allocation-free

2. **Event Subscription Overhead < 1 ms Combined**
   - Limits number of subscribers
   - Event handlers must be lightweight (no heavy processing)
   - Profiler gating for debug info

3. **Headless Support**
   - No Camera.main dependency
   - No scene-specific startup sequences
   - Must work in training environments without GameView

4. **Deterministic Code Paths**
   - No reflection at runtime (all state types known at compile time)
   - No random state selection (only utility-based)
   - RL observation vector must be reproducible

5. **Editor-Gating**
   - All Debug.Log wrapped in `#if UNITY_EDITOR`
   - All Gizmos.Draw wrapped in `#if UNITY_EDITOR`
   - Release builds have zero debug overhead

### **C. Scope Constraints (MVP)**

1. **Single Sector Only**
   - One mission per playthrough
   - Multiple encounters via respawn/retry, not campaign progression

2. **Linear Progression**
   - Explore → KeyAcquired → ExtractionChallenge only
   - No branching, no optional objectives
   - No dynamic rebalancing (fixed thresholds)

3. **No Early Exit Handling**
   - Early exit = failure (TODO for future)
   - No pause/resume, no multi-session persistence

4. **No Nested Objectives**
   - Flat list only in MVP
   - Nesting support deferred to Phase 2+

### **D. Testing & Validation Constraints**

1. **Unit Tests Required**
   - Objective state transitions testable without scene
   - EditMode assembly, no PlayMode required for state logic

2. **Integration Tests Required**
   - Objective → UI/audio flow validation
   - PlayMode tests with GameInitiator & ObjectiveTracker together

3. **Performance Tests Required**
   - Verify zero GC-alloc during objective transitions
   - Profiler comparison before/after implementation

4. **RL Readiness**
   - Observation vector deterministic (no floating-point drift)
   - Reward formula consistent with behavior_upgrades.md patterns
   - Multi-arena training must isolate trackers per arena

**Source Paths:**
- `OBJECTIVE_TRACKER_RESEARCH.md` § "IMPORTANT CONSTRAINTS"
- `doc/Feature_Plans/AI_Performance_Optimization.md`
- `.ralph/objective-tracker-state-machine.md` (MVP scope)

---

## OPEN QUESTIONS / UNKNOWNS (6 Design Decisions Required)

### **1. Extraction Mechanics**
- ❓ How does player trigger extraction? (manual button, auto-jump, reach zone?)
- ❓ Extraction point fixed or random?
- ❓ Can extraction be blocked (by enemies or asteroids)?
- **Impact:** ExtractionChallengeState implementation depends on this

### **2. Failure Conditions**
- ❓ Player destroyed = automatic failure?
- ❓ Time limit exceeded = failure?
- ❓ Enemy escapes = failure?
- ❓ Can player manually abort mission?
- **Impact:** ObjectiveTracker.Tick() must check these conditions

### **3. Wingman System Scope**
- ❓ Full voice system or text-only placeholder?
- ❓ Callouts for all transitions or major events only?
- ❓ Persist across respawns or reset?
- **Impact:** Phase 4 (audio integration) scope & timeline

### **4. Multi-Objective Handling**
- ❓ Sequential (one at a time) or parallel (multiple active)?
- ❓ Nested objectives (sub-goals) or flat list only?
- ❓ One mission per playthrough or campaign arc?
- **Impact:** ObjectiveTracker architecture (single state vs. state stack)

### **5. Retry Logic**
- ❓ Same encounter retry or full GameInitiator reset?
- ❓ Consequences for failure (XP loss, lockout, etc.)?
- ❓ Can player skip missions or must complete in order?
- **Impact:** ObjectiveTracker integration with respawn & scene management

### **6. RL Reward Contract**
- ❓ Which metric drives +0.01 reward/frame? (progress %, time alive, kills?)
- ❓ Completion bonus (+0.5f, +1.0f)?
- ❓ Failure penalty (-0.5f, -1.0f)?
- **Impact:** RLArbiter reward shaping calculation (critical for training)

**Source:** `OBJECTIVE_TRACKER_RESEARCH.md` § "OPEN QUESTIONS / UNKNOWNS"

---

## KEY TECHNICAL DECISIONS MADE

### ✅ Why Emulate AI.States.State Pattern?
- Battle-tested (AI refactor completed Jan 2025)
- Zero allocations (context struct, no reflection)
- Clean lifecycle (Enter/Tick/Exit)
- Already understood by team

### ✅ Why Event-Driven Instead of Polling?
- UI components subscribe (loose coupling)
- Audio system reacts asynchronously
- RL observation tightly integrated
- Respawn-safe re-subscription pattern

### ✅ Why Context Struct Instead of Query Methods?
- Single struct computed once per frame
- Passed as `in` parameter (zero-copy)
- Deterministic (all data in one place)
- Testable without scene context

### ✅ Why Phase 0 Design Review Is Critical?
- 6 open questions affect state logic
- Impacts RL reward shaping (critical for training)
- Prevents rework during implementation
- Team alignment before coding starts

---

## RECOMMENDED IMPLEMENTATION SEQUENCE

**Phase 0 (0.5 days):** Design review — resolve 6 open questions  
**Phase 1 (1 day):** Create ObjectiveState base + 5 concrete states + tests  
**Phase 2 (1 day):** ObjectiveTracker component + GameInitiator integration  
**Phase 3 (1 day):** ObjectiveHUD + UI subscription + re-subscription tests  
**Phase 4 (1 day):** Audio integration + wingman stub  
**Phase 5 (1 day):** RL integration (observation vector + reward)  

**Total: 5 days (MVP)**

**Source:** `OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md` (detailed task breakdown)

---

## SUCCESS CRITERIA

### Code Quality ✅
- All states inherit from ObjectiveState abstract base
- Zero per-frame allocations (context struct only)
- All debug code wrapped in `#if UNITY_EDITOR`
- No reflection at runtime
- Events follow Action<T> delegate pattern

### Testing ✅
- EditMode tests cover all state transitions (unit-testable without scene)
- PlayMode tests cover event emission & UI updates
- UI re-subscription tests verify respawn resilience
- Performance tests confirm zero GC spikes

### Integration ✅
- GameInitiator creates & initializes ObjectiveTracker
- GameConfig extended with ObjectiveParams reference
- ObjectiveHUD subscribes to state changes
- Audio system responds to objective events
- RLArbiter consumes objective observation & reward

### Performance ✅
- Frame time spike < 1 ms on state transition
- GC allocations == 0 per frame (profiler verified)
- Memory footprint < 1 MB total
- Multi-arena training achieves ≥200 FPS headless

---

## RECOMMENDED NEXT READS (WITH SOURCE PATHS)

### For Deep Architecture Understanding
1. `OBJECTIVE_TRACKER_RESEARCH.md` — Full 26 KB research report (30–45 min read)
2. `doc/Feature_Plans/AI_StateSystem_Refactor.md` — State machine spec (template to emulate)
3. `OBSIDIAN_SCOUT_REPORT.md` — Event-driven architecture guide

### For Implementation
1. `OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md` — Step-by-step with code (60–90 min read)
2. `OBJECTIVE_TRACKER_CODEBASE_MAP.md` — Quick reference for file locations (15–20 min read)
3. `src/Asteroids3D/Assets/Scripts/AI/States/State.cs` — Code template to emulate

### For Integration Points
1. `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs` — Initialization orchestration
2. `src/Asteroids3D/Assets/Scripts/Game/GameConfig.cs` — Configuration pattern
3. `doc/Feature_Plans/RL_Implementation_Plan.md` § 9 — RL pipeline requirements

### For Testing Patterns
1. `doc/Feature_Plans/Testing_Plan.md` — Test modalities & infrastructure
2. `src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/EventDrivenRefactorEditModeTests.cs` — Event test patterns
3. `doc/Feature_Plans/AI_Performance_Optimization.md` — Performance constraints

---

## QUICK FACT SHEET

| Aspect | Details |
|--------|---------|
| **MVP States** | 5: Explore, KeyAcquired, ExtractionChallenge, Extracted, Failed |
| **State Pattern** | Emulate AI.States.State (Enter/Tick/Exit + context struct) |
| **Event Model** | Action<ObjectiveState, ObjectiveState> delegates |
| **Context** | Zero-alloc struct with playerPos, hasKey, enemyCount, etc. |
| **Initialization** | Hook into GameInitiator.PresentationReady event |
| **Tick Integration** | ObjectiveTracker.Tick(deltaTime) called each frame |
| **UI Integration** | OnEnable/OnDisable re-subscription pattern |
| **RL Integration** | Observation vector + reward shaping in RLArbiter |
| **Scope** | Single sector MVP (linear Explore → ... → Extracted/Failed) |
| **Timeline** | 5 days (with Phase 0 design review) |
| **Key Constraint** | Zero per-frame allocations |
| **Test Strategy** | EditMode unit tests + PlayMode integration tests |
| **Blocking Dependency** | Phase 0 design decisions (6 open questions) |

---

## NEXT STEPS FOR IMPLEMENTATION TEAM

1. **Schedule Design Review** (0.5 days)
   - Resolve 6 open questions using Phase 0 checklist
   - Lock extraction mechanics, failure conditions, retry logic
   - Align on RL reward contract with ML-Agents team
   - Document decisions in code comments

2. **Create Foundation** (Day 1)
   - Implement ObjectiveState abstract base
   - Create 5 concrete states (Explore, KeyAcquired, ExtractionChallenge, Extracted, Failed)
   - Write unit tests for state transitions
   - Write ObjectiveParams ScriptableObject

3. **Wire to Game Loop** (Day 2)
   - Create ObjectiveTracker MonoBehaviour
   - Hook into GameInitiator.Initialize()
   - Publish ObjectiveStateChanged event
   - Add to GameConfig reference

4. **Integrate UI** (Day 3)
   - Create ObjectiveHUD component
   - Subscribe to state/progress events
   - Implement re-subscription in OnEnable/OnDisable
   - Test with PlayMode test

5. **Add Audio & Polish** (Day 4)
   - Wire objective events to audio system
   - Implement wingman stub (text callouts)
   - Add debug gizmos (extraction zone, key position)

6. **RL Pipeline** (Day 5)
   - Extend observation vector with objective data
   - Implement reward shaping in RLArbiter
   - Test multi-arena isolation
   - Verify zero GC allocations

7. **Final Validation**
   - Run full test suite (EditMode + PlayMode)
   - Profile for GC spikes
   - Headless mode testing
   - RL training dry run

---

**End of Research Summary**

For questions or clarifications, refer to the full research documents:
- `OBJECTIVE_TRACKER_RESEARCH.md` — Comprehensive architecture guide
- `OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md` — Step-by-step code implementation
- `OBJECTIVE_TRACKER_CODEBASE_MAP.md` — Quick reference for file locations & patterns
