# Objective Tracker State Machine — Research & Architecture Guide

**Research Date:** March 3, 2026  
**Scope:** Core progression system for mission-based gameplay  
**Target Audience:** Implementation team designing the objective tracker  
**Status:** Initial research phase; ready for architecture design

---

## OBJECTIVE

Research and document the foundational architecture for an **Objective Tracker State Machine** — a core system that manages missions, objectives, and progression in *Dogfight AIsteroids*. The system should:

1. **Start simple:** MVP with basic state transitions (Explore → KeyAcquired → ExtractionChallenge → Extracted/Failed)
2. **Grow extensible:** Support multiple objective types, nested goals, and dynamic mission structures
3. **Integrate cleanly:** Leverage existing event-driven patterns and state machine infrastructure
4. **Enable progression:** Track game loop outcomes and feed into reinforcement learning pipelines

---

## KEY FINDINGS

### 1. GAME LOOP ARCHITECTURE ✅

**Current State:**
The game currently operates a **single-encounter MVP loop**:
- **GameInitiator** instantiates player ship, enemy AI ship, and asteroid field
- Ships engage in free-form dogfighting with no explicit objectives or mission constraints
- Game loop continues until one ship is destroyed or player exits
- No inherent sense of progression, mission completion, or "win conditions" beyond survival

**Relevant Code:**
- `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs` — initializes encounters
- `src/Asteroids3D/Assets/Scripts/Game/GameConfig.cs` — configures player/enemy/arena
- `src/Asteroids3D/Assets/Scripts/Ships/Ship.cs` — ship lifecycle (spawn, damage, death)

**Key Integration Points:**
- **GameInitiator.PresentationReady** event — fires when scene is ready for gameplay
- **ShipRegistry** — tracks active ships (player, enemies); provides observer pattern for add/remove
- **Respawn system (ShipRespawnRunner)** — already handles ship death and revival

---

### 2. STATE MACHINE PATTERNS (EXISTING) ✅

**AI State Machine Fully Implemented (Jan 2025 Refactor)**

The codebase already has a **well-architected finite state machine** serving as a template:

**Architecture:**
```csharp
public abstract class State {
    protected Navigator navigator;
    protected Gunner gunner;
    protected UtilityTuning utilityTuning;
    
    public virtual void Enter(Info ctx) { }
    public abstract void Tick(Info ctx, float deltaTime);
    public virtual void Exit() { }
    public abstract float ComputeUtility(Info ctx);
}
```

**Key Pattern Components:**
- **State Type Enum** — defines discrete states (Idle, Patrol, Attack, Evade, Kite, Orbit, JinkEvade)
- **Context Struct** — single lightweight struct passed `in` each frame (zero-copy, no allocations)
  - Contains: `shieldPct`, `relDistance`, `relSpeed`, `lineOfSight`, `incomingMissile`, `nearbyFriendCount`
- **Utility-Based Selection** — state machine selects highest-utility state each frame
- **Lifecycle Callbacks** — `Enter()` / `Tick(dt)` / `Exit()` provide natural event points
- **No Reflection** — zero runtime type discovery; fully deterministic code paths

**Why This Pattern Works for Objective Tracker:**
- Separation of concerns (Enter/Tick/Exit) maps naturally to objective lifecycle
- Dependency injection pattern enables loose coupling and testing
- Utility scoring can drive objective priority/feasibility
- Context struct pattern scales efficiently for nested objectives

**Source:**
- `doc/Feature_Plans/AI_StateSystem_Refactor.md` — Full specification
- `src/Asteroids3D/Assets/Scripts/AI/States/State.cs` — Abstract base
- `src/Asteroids3D/Assets/Scripts/AI/States/` — Concrete implementations

---

### 3. EVENT-DRIVEN PATTERNS (PARTIALLY IMPLEMENTED) ⚠️

**Current Status:**
The codebase has **strong event-driven foundations** but is still mid-refactor toward full event separation:

**Implemented:**
- **Lock State Events** (`LockController.OnStateChanged`) — weapon lock transitions emit events
- **Heat Events** (`Heat.OnHeatChanged`) — weapon heat level changes notify subscribers
- **UI Event Subscriptions** — `LockOnIndicator`, `MissileAmmoUI`, `LaserHeatUI` subscribe to weapon state events
- **Audio Events** — weapon fire, impacts, ambient audio systems can subscribe to game events

**In-Progress:**
- **ShipEvents Facade** (planned) — centralized event source for `OnDamageDealt`, `OnDestroyed`, `OnShieldChanged`
- **GameStateEventsFacade** (planned) — aggregates all game-logic events for UI/audio separation
- **Audio Event System** (not yet implemented) — needs routing from weapon/damage events to audio manager

**Key Pattern:**
- Event publishers use `public event Action<StateFrom, StateTo>` delegates
- Subscribers register in `OnEnable()` and unregister in `OnDisable()` to handle respawn cycles
- No global event bus; arena-scoped or component-scoped events prevent cross-contamination

**Relevant Test:**
- `src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/EventDrivenRefactorEditModeTests.cs` — validates event emission and UI subscription patterns

**Blocking Dependencies:**
- ML-Agents RL pipeline (due Jun 23) requires `ShipEvents.cs` & `GameStateEventsFacade`
- Objective tracker should **emit events** at each state transition (ObjectiveStateChanged, ObjectiveCompleted, etc.)

---

### 4. MISSION/OBJECTIVE STRUCTURE (NOT YET DESIGNED) ⚠️

**Current Gap:**
The codebase has **no existing mission or objective tracker**. However, the Proposal and roadmap suggest a future multi-sector campaign:

**Inferred from Proposal & Roadmap:**
- **Encounters** are time-bound combat scenarios (player vs AI enemy)
- **Missions** likely bundle multiple encounters with linked objectives
- **Sectors** represent spatial regions with distinct asteroid densities, enemy types, and loot
- **Progression** advances through sectors, unlocks weapon types, upgrades, or difficulty tiers
- **Objective Types** (inferred from context):
  - **Explore:** Scout sector, identify threats
  - **KeyAcquired:** Collect artifact/resource
  - **ExtractionChallenge:** Survive enemy pursuit while escaping
  - **Extracted/Failed:** Terminal states

**Handoff from .ralph/objective-tracker-state-machine.md:**
```
States: Explore → KeyAcquired → ExtractionChallenge → Extracted | Failed

Key Constraints:
- Single-sector MVP loop
- Non-destructive: keep existing behavior accessible
- Wingman persistence = stub/placeholder (for later)
- Early exit = failure (TODO for later)
- Extraction gated behind KeyAcquired state
```

**Expected Integration:**
- Missions tied to **GameConfig** (like player/enemy templates are)
- Objective state changes trigger **UI updates** (HUD, objective log)
- **Audio cues** on objective completion (wingman callouts, mission complete sfx)
- **RL observation/reward** tied to objective progress

---

### 5. DEPENDENCY INJECTION & TESTING PATTERNS ✅

**Established Patterns:**
- Constructor injection for state components (`Navigator`, `Gunner`, `UtilityTuning`)
- Field injection via `Initialize(dep1, dep2)` methods for complex setups
- ScriptableObject assets for tunables (e.g., `StateParams`, `GameConfig`)
- No singletons; all services passed explicitly

**For Objective Tracker:**
- Inject `ShipRegistry`, `GameInitiator`, `EventPublisher` into tracker
- Use `ObjectiveParams` ScriptableObject for tuning completion thresholds, timeouts, etc.
- Testable via mocked dependencies (no scene context required)

---

### 6. PERFORMANCE & CONSTRAINTS ✅

**Critical Requirements (from AI_Performance_Optimization.md):**
- **Zero per-frame allocations:** Objective state updates must not GC-alloc
- **Event subscription overhead:** < 1 ms combined per frame
- **Editor-gating:** All debug logging/gizmos wrapped in `#if UNITY_EDITOR`
- **Headless builds:** Objective tracker must work without `Camera.main`

**Context Struct Pattern (Zero Allocations):**
Can apply same pattern to objectives:
```csharp
struct ObjectiveContext {
    public ObjectiveType type;
    public float progressPct;
    public float timeElapsed;
    public bool isActive;
    // ... other relevant fields
}
```

---

### 7. UI & AUDIO INTEGRATION PATTERNS ✅

**UI Separation Strategy (from OBSIDIAN_SCOUT_REPORT.md):**
1. Objective tracker publishes state-change events
2. UI components (HUD, objective log) subscribe to `ObjectiveStateChanged` event
3. Components re-subscribe in `OnEnable()` to handle reset cycles
4. UI reads objective data on-demand (no polling)

**Audio Integration:**
1. Objective complete event → play "mission complete" sfx
2. Objective failed event → play "mission failed" sfx
3. Wingman system (stub) can narrate objective transitions

**Example Event Flow:**
```
ObjectiveTracker.Enter(Explore) 
  → ObjectiveStateChanged(None, Explore) 
  → UI updates HUD objective text
  → Audio plays ambient music cue
  → Wingman says "Engaging exploration mode"
```

---

### 8. ML-AGENTS & RL INTEGRATION PATHWAY ⚠️

**Blocking Dependencies:**
The RL pipeline (due Jun 23) requires:
- `ShipEvents.cs` — publishes damage, destruction, objectives
- `GameStateEventsFacade` — aggregates events for observers
- `ObjectiveTracker` — provides observation vector (current objective, progress %)

**Data Contract for RL:**
```
Observation Size = 32 (existing) + |ObjectiveContext|
  + objective type (discrete)
  + objective progress (float 0–1)
  + time in objective (float)
  + active objective count (int)

Reward Shaping = +0.01 * (objective progress) per frame
                + bonus for objective completion
                + penalty for objective failure

Action Space = can bias toward objectives (future)
```

**Integration Point:**
- `RLArbiter.cs` (stub, already created) should consume objective events
- Multi-arena training requires arena-scoped objective trackers (not global)

---

## IMPORTANT CONSTRAINTS

### **A. Architectural**
1. **State machine must follow AI state pattern:** Enter/Tick/Exit lifecycle with context struct
2. **Zero global state:** Arena-scoped objectives only; multi-arena training requires isolation
3. **Event-driven:** Objective tracker must publish events, not push updates to UI
4. **Non-destructive:** Existing game loop must continue working without refactor
5. **Wingman persistence stub:** Placeholder for future narrative/companion system

### **B. Performance**
1. **Zero per-frame allocations:** Use context struct, object pooling for objectives
2. **Event subscription overhead:** < 1 ms combined
3. **Headless support:** No Camera.main or scene-specific dependencies
4. **Deterministic paths:** No reflection; compiled-out debug code

### **C. Scope (MVP)**
1. **Single sector only:** Multiple encounters, one mission per playthrough
2. **Linear progression:** Explore → KeyAcquired → ExtractionChallenge only
3. **No branching:** Early exit = failure (revisit later)
4. **No dynamic rebalancing:** Fixed thresholds (TODO: tunable via ScriptableObject)

### **D. Testing & Validation**
1. **Unit tests required:** Objective state transitions testable without scene
2. **Integration tests required:** Objective → UI/audio flow validation
3. **Performance tests required:** Verify zero GC-alloc during objective transitions
4. **RL readiness:** Observation vector must be deterministic

---

## OPEN QUESTIONS / UNKNOWNS

1. **Wingman System:**
   - How should wingman callouts be triggered? (objective complete event, random interval, or explicit call?)
   - Is wingman tied to player ship or global presence?
   - Persistence across respawns? (stub = no, but important for future design)

2. **Nested Objectives:**
   - Single-level objectives for MVP, but how to structure for multi-level in future?
   - Example: Mission "Survive the Asteroid Belt" contains 5 sub-objectives (explore sectors A-E)
   - Should parent objective track aggregate progress of children, or independent?

3. **Early Exit / Failure Handling:**
   - When does an objective fail? (player dies, time expires, enemy escapes, extraction blocked?)
   - Can player retry the same sector/objective, or does it lock out?
   - What happens to mission state if player exits early?

4. **Extraction Mechanics:**
   - How is "extraction" triggered? (manual escape pod, jump gate, ship escape?)
   - Is extraction always possible after KeyAcquired, or blocked by enemy/asteroids?
   - What's the win condition? (escape with key, escape with score threshold, survive X time?)

5. **Objective Types Beyond MVP:**
   - How many distinct objective types? (Explore, Defend, Retrieve, Survive, Escort, etc.)
   - Are they mutually exclusive per mission, or can multiple objectives coexist?
   - How to extend for future campaign/multiplayer missions?

6. **Data Persistence:**
   - Should objective progress persist across scenes/sessions?
   - Does player earn XP/rewards from objective completion?
   - Connection to RL reward shaping? (what metric = +0.01 reward per frame?)

7. **UI Representation:**
   - Objective log (text-based list), compass markers, HUD indicator, or all three?
   - How to display nested/parallel objectives in compact HUD?
   - Wingman portrait + voice lines, or text-only callouts?

8. **Mission Failure Retry:**
   - Can player restart objective without reloading scene?
   - Or must they respawn/reset and re-enter GameInitiator loop?
   - How does respawn system (ShipRespawnRunner) interact with objective retry?

---

## RECOMMENDED NEXT READS (WITH PATHS)

### **For State Machine Deep-Dive:**
1. `doc/Feature_Plans/AI_StateSystem_Refactor.md` — Full state machine spec & context struct pattern
2. `doc/Feature_Plans/AI_StateSystem_Refactor_Summary.md` — Implementation record (COMPLETED)
3. `src/Asteroids3D/Assets/Scripts/AI/States/State.cs` — Abstract base to emulate
4. `src/Asteroids3D/Assets/Scripts/AI/States/` — Concrete state implementations for reference

### **For Event-Driven Patterns:**
1. `OBSIDIAN_SCOUT_REPORT.md` (this repo) — Comprehensive event architecture guide
2. `doc/Feature_Plans/RL_Implementation_Plan.md` § 9 — Codebase prep tasks (ShipEvents, GameStateEventsFacade)
3. `src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/EventDrivenRefactorEditModeTests.cs` — Event patterns & testing

### **For Game Loop & Integration:**
1. `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs` — Initialization choreography
2. `src/Asteroids3D/Assets/Scripts/Game/GameConfig.cs` — Config object pattern
3. `doc/Feature_Plans/Asteroid_Environment_Update.md` — Multi-arena architecture (training context)

### **For Performance & Testing:**
1. `doc/Feature_Plans/AI_Performance_Optimization.md` — Performance ceiling & scaling
2. `doc/Feature_Plans/Testing_Plan.md` — Test modalities (EditMode, PlayMode, Performance)
3. `doc/Feature_Plans/General_Optimizations.md` — Editor-gating, pooling patterns

### **For Behavior & Tuning:**
1. `doc/Feature_Plans/Behavior_Upgrades.md` — State tuning pattern & RL integration points
2. `doc/Proposal.md` — Project goals & play-testing protocols

---

## ARCHITECTURE PROPOSAL (INITIAL)

### **Phase 0: Foundation (Design)**
```csharp
// ObjectiveTracker.cs — Main system
public class ObjectiveTracker : MonoBehaviour {
    // State machine structure
    private ObjectiveState currentState;
    private ObjectiveContext context;
    
    // Events (pattern from ShipEvents)
    public event Action<ObjectiveState, ObjectiveState> OnStateChanged;
    public event Action<ObjectiveType, float> OnProgressChanged;
    public event Action<ObjectiveState> OnObjectiveComplete;
    
    // Lifecycle
    public void Initialize(GameConfig config) { }
    public void Tick(float deltaTime) { }
    public void Reset() { }
}

// ObjectiveState.cs — Base state class (emulate AI State pattern)
public abstract class ObjectiveState {
    protected readonly ObjectiveTracker tracker;
    protected readonly ObjectiveParams parameters;
    
    public virtual void Enter(ObjectiveContext ctx) { }
    public abstract void Tick(ObjectiveContext ctx, float deltaTime);
    public virtual void Exit() { }
    public abstract ObjectiveState GetNextState(ObjectiveContext ctx);
    public abstract float ComputeUtility(ObjectiveContext ctx);
}

// Concrete states for MVP
public class ExploreState : ObjectiveState { /* impl */ }
public class KeyAcquiredState : ObjectiveState { /* impl */ }
public class ExtractionChallengeState : ObjectiveState { /* impl */ }
public class ExtractedState : ObjectiveState { /* terminal */ }
public class FailedState : ObjectiveState { /* terminal */ }

// ObjectiveContext.cs — Zero-alloc context struct
public struct ObjectiveContext {
    public ObjectiveType type;
    public float progressPct; // 0–1
    public float timeElapsed;
    public Vector3 playerPos;
    public Vector3 extractionPoint;
    public bool hasKey;
    public int enemyCount;
    public float asteroidDensity;
}

// ObjectiveParams.cs — Tunable asset
[CreateAssetMenu]
public class ObjectiveParams : ScriptableObject {
    public float exploreCompletionThreshold = 0.8f;
    public float extractionTimeLimit = 300f;
    public float keySpawnRadius = 50f;
    // ... more tunables
}
```

### **Phase 1: Integration (MVP)**
1. Create objective tracker component
2. Hook into GameInitiator.PresentationReady event
3. Emit ObjectiveStateChanged event when state transitions
4. Wire UI (objective HUD) to subscribe to state changes
5. Write unit tests for state transitions (EditMode)
6. Write integration tests for UI flow (PlayMode)

### **Phase 2: Extension (Future)**
1. Support multiple objectives per mission
2. Add objective nesting / sub-goals
3. Implement dynamic difficulty scaling
4. Integrate wingman callout system
5. Add mission persistence & retry logic

---

## HANDOFF SUMMARY FOR IMPLEMENTATION TEAM

### **What We Know:**
✅ State machine pattern is battle-tested in AI system (Jan 2025 refactor)  
✅ Event-driven architecture in progress (ShipEvents, UI subscriptions working)  
✅ GameInitiator & GameConfig provide clean initialization hooks  
✅ ShipRegistry & respawn system handle lifecycle events  
✅ Zero-alloc context struct pattern proven in AI state selection  
✅ Performance constraints & editor-gating conventions established  

### **What We Don't Know (Design Decisions Needed):**
⚠️ Extraction mechanics (how does player escape the sector?)  
⚠️ Failure conditions (what triggers objective failure?)  
⚠️ Wingman system scope (stub vs. full voice/narrative)  
⚠️ Multi-objective handling (parallel, nested, or sequential only?)  
⚠️ Retry logic (same encounter or reset required?)  
⚠️ RL reward contract (what metric drives +0.01 per frame?)  

### **Recommended Implementation Sequence:**

1. **Day 1 – Foundation**
   - Define `ObjectiveState` abstract base (emulate `AI.States.State`)
   - Create `ExploreState`, `KeyAcquiredState`, `ExtractionChallengeState`, terminal states
   - Implement state transition logic (no events yet)
   - **Test:** Unit tests for state transitions (EditMode assembly)

2. **Day 2 – Events & Initialization**
   - Add `OnStateChanged`, `OnProgressChanged`, `OnObjectiveComplete` events
   - Create `ObjectiveTracker` component and `ObjectiveParams` ScriptableObject
   - Hook tracker into `GameInitiator.PresentationReady`
   - Wire objective tracker to respawn system
   - **Test:** Integration test for initialization flow

3. **Day 3 – UI Integration**
   - Create objective HUD component
   - Subscribe to `OnStateChanged` and `OnProgressChanged` events
   - Implement re-subscription in `OnEnable()` for respawn resilience
   - **Test:** PlayMode test for UI update on state changes

4. **Day 4 – Audio & Polish**
   - Wire objective events to audio system (sfx for completion, failure)
   - Implement wingman stub (placeholder for future)
   - Add debug gizmos (show objective zone, extraction point, etc.)
   - **Test:** PlayMode test for audio/wingman flow

5. **Day 5 – RL Integration**
   - Add `ObjectiveContext` to observation vector
   - Calculate reward bonus for objective completion
   - Wire `RLArbiter` to objective events
   - **Test:** Verify observation vector and reward in training setup

### **Success Criteria:**
- State transitions work without allocations (zero GC spike)
- UI updates correctly on objective state changes
- Audio/wingman respond to objective events
- All transitions unit-testable without scene context
- PlayMode integration tests pass (EditMode + PlayMode)
- RL observation vector includes objective data
- Headless mode (no Camera.main required)

### **Risk Mitigations:**
1. **State bloat:** Use `ObjectiveParams` ScriptableObject to avoid enum explosion
2. **Event subscription bugs:** Implement re-subscription tests (like `ShipChildComponentStatePlayModeTests`)
3. **RL observation inconsistency:** Unit test observation struct serialization
4. **Extraction mechanics unclear:** Start with simple "escape to edge of arena" and extend later

---

## NEXT STEPS

1. **Schedule design review:** Resolve open questions (wingman scope, extraction mechanics, retry logic)
2. **Create ObjectiveState abstract base:** Model after `src/Asteroids3D/Assets/Scripts/AI/States/State.cs`
3. **Scaffold test assembly:** Create `Assets/Tests/PlayMode/ObjectiveTrackerPlayModeTests.cs`
4. **Prototype MVP states:** Explore → KeyAcquired → ExtractionChallenge → terminal states
5. **Wire to GameInitiator:** Modify initialize routine to create and start tracker
6. **Implement UI subscriber:** Create objective HUD component and event subscriptions
7. **Run first test pass:** Verify state transitions, events, and UI updates

---

**End of Research Report**  
*For questions or clarifications, refer to source paths and the OBSIDIAN_SCOUT_REPORT.md for event-driven architecture context.*
