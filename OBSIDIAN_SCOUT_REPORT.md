# Research Summary: Event-Driven Game Logic Refactoring
## UI/Audio/Gizmo Separation Architecture

**Scout Mission:** Find relevant Obsidian notes on refactoring main game logic toward event-driven architecture with UI/audio/gizmo separation.  
**Research Date:** 2025-03-02  
**Target Audience:** Refactoring planners and game-logic developers

---

## OBJECTIVE

Identify current architectural conventions, prior decisions, and existing patterns relevant to refactoring the game logic into an event-driven system that cleanly separates:
- **Game logic** (state machines, AI, combat)
- **UI systems** (LockOnIndicator, ShieldUI)
- **Audio systems** (weapon fire, impacts, ambient)
- **Debug visualization** (gizmos, editor tools)

---

## KEY FINDINGS

### 1. STATE SYSTEM ARCHITECTURE ✅ **RECENTLY REFACTORED**

**Status:** Full migration from Behavior Trees to Finite State Machine completed (Jan 2025).

**Architecture:**
- **Dependency Injection Pattern:** States receive injected `AINavigator` and `AIGunner` references
- **Utility-Based Selection:** State machine selects highest-utility state each frame via `ComputeUtility(AIContext ctx)`
- **Context Struct Pattern:** Lightweight `AIContext` computed once per frame and cached, containing:
  - `shieldPct`, `relDistance`, `relSpeed`, `lineOfSight`, `incomingMissile`, `nearbyFriendCount`
- **Lifecycle Pattern:** States implement `Enter()`, `Tick(dt)`, `Exit()` callbacks
- **No Reflection:** Zero runtime type discovery overhead

**Key Benefits for Event-Driven Refactor:**
- States emit clear boundaries between logic phases (Enter/Tick/Exit) = natural event points
- Context struct is passed consistently, enabling decoupled state queries
- No global enum state tracking = easier to add event hooks

**Source:**
- `doc/Feature_Plans/AI_StateSystem_Refactor.md` — Full architecture spec
- `doc/Feature_Plans/AI_StateSystem_Refactor_Summary.md` — Implementation record (COMPLETED)

**File Locations in Codebase:**
- `Assets/Scripts/AI/States/AIState.cs` — Abstract base
- `Assets/Scripts/AI/States/AIStateMachine.cs` — State selection logic
- `Assets/Scripts/AI/States/{IdleState,PatrolState,EvadeState,AttackState}.cs`

---

### 2. EVENT-DRIVEN PATTERNS (PARTIAL / PLANNED)

#### **A. Game Logic → Reward/Observation Events** 

**Planned for ML-Agents Integration (§9 Codebase Prep Tasks):**
- **ShipEvents.cs (NEW):** Expose `OnDestroyed`, `OnDamageDealt` events
- **ShipDamageHandler.cs & Asteroid.cs:** Publish damage/destruction events so subscribers (UI, audio, RL) can react without reflection
- **Status:** Identified as blocking task C-1, required by Jun 23 2025

**Source:** `doc/Feature_Plans/RL_Implementation_Plan.md` § 9 (Codebase Preparation Tasks)

---

#### **B. UI Event Subscriptions** 

**Current Pattern (with Issues):**
- **LockOnIndicator:** Subscribes to weapon lock events; needs post-reset re-subscription
- **ShieldUI:** Subscribes to shield value change events; can disconnect on death/reset
- **Both:** Respond to state changes via events rather than polling

**Issue Identified:**
- Event subscriptions sometimes fail to re-register after `death → reset` cycles
- Fixed via diagnostic components (`UIDiagnostics.cs`) that verify subscription state

**Best Practice (from diagnostics):**
- Components should re-subscribe in `OnEnable()` or via explicit reset hooks
- Guard against disabled components receiving events

**Source:**
- `doc/Feature_Plans/DIAGNOSTIC_IMPLEMENTATION_SUMMARY.md` — Full diagnostic suite
- Test cases: `ShipChildComponentStatePlayModeTests.cs` (references `AfterShipReset_ShieldUIRespondsToShieldDamage`, `AfterShipReset_LockOnIndicatorRespondsToLockEvents`)

**File Locations:**
- `Assets/Scripts/Diagnostics/UIDiagnostics.cs` — Monitors UI event subscriptions
- `Assets/Scripts/Diagnostics/ComponentStateDiagnostics.cs` — Tracks enable/disable state

---

#### **C. Audio System Patterns** 

**Current Optimization (NOT Event-Driven Yet):**
- **Audio One-Shot Pooling:** Pooled `AudioSource` replaces `AudioSource.PlayClipAtPoint` to reduce GameObject churn
- **Status:** Implemented as general optimization (not integrated with game logic events yet)

**Opportunity for Event-Driven Integration:**
- Weapon fire events → audio system subscription
- Shield/damage events → sound effects
- State transitions → ambient audio or SFX cues (mentioned as future enhancement)

**Source:** `doc/Feature_Plans/General_Optimizations.md` § 5 (Audio one-shot pooling)

---

### 3. DEBUG VISUALIZATION (GIZMOS) ARCHITECTURE

#### **Current Issues Identified**

**Problem:** Scattered `OnDrawGizmos*` blocks across multiple systems (AI nodes, camera, arena, missiles, weapons)
- Repetitive scaffolding in every Behavior Tree action/condition node
- Inconsistent color palettes and drawing conventions
- No central authority for debug visualization

**Source:** `doc/Feature_Plans/Refactor.md` (Roadmap)

#### **Planned Refactoring**

**Proposed Pattern:**
- **DebugGizmos Service (NEW):** Centralized helper with methods like:
  - `DrawSphereLabel(Vector3 pos, float radius, string label, Color color)`
  - Consistent color palette across all systems
- **State-Specific Gizmos:** Each `AIState` class can request visualization through injected `IDebugGizmoService`
- **Editor-Gating:** All gizmo code wrapped in `#if UNITY_EDITOR`

**In-Editor Gizmo Displays:**
- Current state & utility scores for each ship (already in `AIStateMachine`)
- State-specific parameters (distance to target, strafe radius, etc.)
- Debug overlay (OnGUI) showing current state & major param values

**Source:**
- `doc/Feature_Plans/Refactor.md` (Item 7: "Gizmo / debug UI sprawl")
- `doc/Feature_Plans/Refactor.md` (Item 1: "Behaviour-Tree boilerplate")
- `doc/Feature_Plans/AI_StateSystem_Refactor.md` § 4 (Phase 4 Polish)
- `doc/Feature_Plans/Behavior_Upgrades.md` § 4 (Integration Steps)

---

### 4. ARCHITECTURAL CONVENTIONS & PATTERNS

#### **A. Dependency Injection**
- **Pattern:** Constructor or field injection for required services
- **Examples:**
  - `AIState(AINavigator nav, AIGunner gunner)`
  - `StateParams` assets hold tunables, injected into state constructors
  - IDebugGizmoService (future)

**Rationale:** Enables unit testing, loose coupling, easy mocking

---

#### **B. Context Struct Pattern**
- **Pattern:** Single immutable struct (`AIContext`) computed once per frame
- **Passed as `in` param:** Zero-copy value type to all consumers
- **Contains:** All relevant sensor data (positions, health, threats, etc.)

**Rationale:** Reduces per-frame allocations, deterministic observation data

**Source:** `doc/Feature_Plans/AI_StateSystem_Refactor.md` § 3, `doc/Feature_Plans/Behavior_Upgrades.md` § 2

---

#### **C. Editor-Gating Convention**
- **Pattern:** `#if UNITY_EDITOR` guards on all debug logging, gizmo drawing, and unsafe reflection
- **Status:** Partially applied; General Optimizations task completed this in § 2

**Example:**
```csharp
#if UNITY_EDITOR
    Debug.Log("Current state: " + CurrentState.GetType().Name);
    Gizmos.DrawSphere(transform.position, 1f);
#endif
```

**Source:** `doc/Feature_Plans/General_Optimizations.md` § 2 (Editor-gate verbose logging)

---

#### **D. Lifecycle Management**
- **MonoBehaviour → State Machine:** Initialization in `InitializeCommander()` or `Awake()`
- **Per-Frame Updates:** Ordered sequence in `FixedUpdate()`
  1. Gather observations (`AIContextProvider`)
  2. Compute utility & select state
  3. Tick current state
  4. Apply movement/weapons output
- **Reset Lifecycle:** Components re-subscribe to events on `OnEnable()` or explicit reset hook

**Source:** `doc/Feature_Plans/AI_StateSystem_Refactor_Summary.md` § Changes Implemented

---

### 5. MULTI-ARENA ARCHITECTURE (Training-Specific)

**Pattern:** Self-contained arena prefabs enable parallel instance simulation
- **Arena = {Agent, Enemy, AsteroidField, Boundaries}**
- **Decoupled from Camera.main** (critical for headless training)
- **Sector Manager** spawns independent asteroid fields per arena

**Implications for Event-Driven Design:**
- Each arena should have isolated event channels (no global event bus)
- Reward/observation events scoped to arena instance
- UI/audio updates only affect the active viewport (or head-less arena manager)

**Source:** `doc/Feature_Plans/Asteroid_Environment_Update.md` § 1-2 (Motivation & Goals)

---

## IMPORTANT CONSTRAINTS

### **A. Performance**
1. **Zero-reflection requirement:** State system must compile to deterministic code paths
2. **Per-frame allocation ceiling:** Context struct passed `in` (stack, not heap)
3. **Event subscription overhead:** UI/audio event handlers must complete in < 1ms combined
4. **Gizmo/Debug overhead:** All visualization must be editor-gated and incur zero runtime cost

**Source:** `doc/Feature_Plans/AI_Performance_Optimization.md`

### **B. Platform Compatibility**
1. **Headless builds:** Arena logic must work without `Camera.main`
2. **Editor + Runtime:** Debug gizmos and logs require dual-mode logic
3. **Multi-threaded Physics:** Jobified event handling may require `NativeArray` containers instead of managed lists

**Source:** `doc/Feature_Plans/Asteroid_Environment_Update.md` § 1 (Motivation)

### **C. Testing & Validation**
1. **Event subscription tests:** Must verify UI components re-subscribe after death/reset
2. **Diagnostic components:** Mandatory for identifying intermittent event-handling bugs
3. **Integration tests:** Multi-arena scenarios must not cross-contaminate events

**Source:** `doc/Feature_Plans/DIAGNOSTIC_IMPLEMENTATION_SUMMARY.md` (Testing Checklist)

---

## OPEN QUESTIONS / UNKNOWNS

1. **Audio Event System:**
   - No concrete `ShipAudioEvents` or centralized audio event bus found yet
   - How should weapon fire, impacts, and ambient audio subscribe? (event, audio mixer control, or polling?)
   - Should audio manager be injected into states or remain a global listener?

2. **Gizmo Service Interface:**
   - `DebugGizmos` helper planned but not yet implemented
   - Should it be a static utility, injected service, or singleton?
   - How to handle state-specific gizmo parameters (colors, sizes) at scale?

3. **UI Separation from Game Logic:**
   - Current UI components (LockOnIndicator, ShieldUI) subscribe to events, but event sources are scattered
   - Should there be a `GameStateEvents` facade that all UI subscribes to?
   - How to handle UI that responds to multiple game logic events (e.g., shield + lock-on combined display)?

4. **Event Ordering Guarantees:**
   - When a ship takes damage and fires a weapon simultaneously, what order do events publish?
   - Do we need explicit phase ordering (damage phase → audio phase → visual phase)?

5. **Arena-Scoped vs. Global Events:**
   - Should multi-arena scenarios share a global event bus or have isolated buses per arena?
   - How to prevent cross-arena event crosstalk in training scenarios?

---

## RECOMMENDED NEXT READS

1. **For State Machine deep-dive:**
   - `doc/Feature_Plans/AI_StateSystem_Refactor.md` (full spec) + `AI_StateSystem_Refactor_Summary.md` (implementation record)

2. **For RL integration & event hooks:**
   - `doc/Feature_Plans/RL_Implementation_Plan.md` § 9 (Codebase Preparation Tasks C-1…C-4)

3. **For refactoring roadmap context:**
   - `doc/Feature_Plans/Refactor.md` (high-ROI quick wins)
   - `doc/Features_Todo.md` (sequencing & priority by date)

4. **For UI/audio diagnostics:**
   - `doc/Feature_Plans/DIAGNOSTIC_IMPLEMENTATION_SUMMARY.md` (test cases + diagnostic components)

5. **For optimization constraints:**
   - `doc/Feature_Plans/AI_Performance_Optimization.md` (performance ceiling & scaling)
   - `doc/Feature_Plans/General_Optimizations.md` (editor-gating, pooling patterns)

6. **For behavior/state design:**
   - `doc/Feature_Plans/Behavior_Upgrades.md` (future states & tunable params pattern)

---

## HANDOFF SUMMARY FOR REFACTORING PLANNER

### Current State (As of Mar 2025)

✅ **Complete:**
- Finite State Machine architecture (replacing BT)
- Dependency injection patterns for state components
- Context struct for deterministic observation passing
- Editor-gating convention for debug code
- Audio pooling optimization
- Diagnostic components for UI event subscription tracking

⚠️ **Identified but Not Yet Implemented:**
- Centralized `DebugGizmos` helper (scattered `OnDrawGizmos*` blocks remain)
- `ShipEvents.cs` with `OnDestroyed`, `OnDamageDealt` events
- Unified UI event subscription pattern (currently per-component)
- Audio event system (no central audio event bus)

### Recommended Refactoring Sequence

1. **Phase 1 – Event Foundation (1 day)**
   - Create `ShipEvents.cs` interface/class with `OnDamageDealt`, `OnDestroyed`, `OnShieldChanged` events
   - Modify `ShipDamageHandler.cs` and `Asteroid.cs` to publish these events
   - Create `GameStateEventsFacade` that aggregates all game-logic events
   - **Acceptance:** UI can subscribe to centralized facade; diagnostic tests pass

2. **Phase 2 – UI Event Separation (1 day)**
   - Refactor `LockOnIndicator` & `ShieldUI` to subscribe to `GameStateEventsFacade` (not direct component references)
   - Add re-subscription logic in `OnEnable()` to handle death/reset cycles
   - Ensure components gracefully degrade if events are null
   - **Acceptance:** Multi-reset cycle tests pass without event subscription loss

3. **Phase 3 – Audio Event System (0.5 day)**
   - Create `AudioEventManager` that subscribes to `ShipEvents`
   - Route weapon fire → SFX, impacts → impact audio, state transitions → ambient cues
   - Leverage existing pooled `AudioSource` infrastructure
   - **Acceptance:** No frame spike from audio, consistent pooling

4. **Phase 4 – Gizmo Centralization (0.5 day)**
   - Implement `DebugGizmoService` with `DrawSphereLabel()`, `DrawArrow()`, etc.
   - Inject into `AIState` constructors; remove scattered `OnDrawGizmos*` blocks
   - Guard all draws with `#if UNITY_EDITOR`
   - **Acceptance:** Gizmo performance ≥ current (inline vs. service call), consistent look

### Key Success Metrics

- **Event latency:** < 1ms from game-logic event publish to UI/audio subscriber callback
- **GC pressure:** Zero allocations from event system per frame (use structs, pooled arrays)
- **Testability:** All state transitions + event sequences unit-testable without scene context
- **Debuggability:** Gizmo display identifies current state & utilities without console spam

### Blocking Dependencies

- **RL Pipeline Tasks C-1...C-3** (Jun 23) depend on `ShipEvents.cs` & `GameStateEventsFacade`
  - See `doc/Feature_Plans/RL_Implementation_Plan.md` § 9

### Risk Mitigations

1. **Event subscription cycles:** Implement re-subscription tests (like `ShipChildComponentStatePlayModeTests`)
2. **Cross-arena contamination:** Use instance-scoped event buses (not static/global)
3. **Editor vs. Runtime:** Gate all gizmo/log code with `#if UNITY_EDITOR` before shipping

---

**End of Report**  
*For questions or clarifications, refer to source note paths above.*
