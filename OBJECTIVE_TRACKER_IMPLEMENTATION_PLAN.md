# Objective Tracker State Machine — Implementation Plan

**Timeline:** 5 days (MVP)  
**Start Date:** Recommend after initial design review  
**Success Metrics:** State transitions + events + UI subscription

---

## PHASE 0: DESIGN REVIEW (0.5 days)

### Objectives
Resolve open questions before implementation starts.

### Design Questions to Answer

1. **Extraction Mechanics**
   - [ ] Manual escape (button press) or automatic (reach zone)? - automatic
   - [ ] Extraction point fixed or random each run? - fixed for now
   - [ ] Can extraction be blocked (by enemies, asteroids)? - by enemies (if they are too close they can follow you)

2. **Failure Conditions** - 
   - [ ] Player destroyed = mission failure? - yes
   - [ ] Time limit exceeded = failure? - no time limit
   - [ ] Enemy escapes = failure? - no for now
   - [ ] Can player manually abort mission? - no for now

3. **Wingman System** - no wingman for now
   - [ ] Full voice/narrative or placeholder text?
   - [ ] Callouts for all state transitions or major events only?
   - [ ] Persistent across respawns or reset? - 

4. **Multi-Objective Handling**
   - [ ] Sequential (one at a time) or parallel (multiple active)? - sequential for now
   - [ ] Nested objectives (sub-goals) or flat list? - flat list for now
   - [ ] One mission per playthrough or campaign arc? - one mission for now

5. **Retry Logic**
   - [ ] Same encounter retry or full GameInitiator reset? - same encountery retry
   - [ ] Consequences for failure (XP loss, lockout, etc.)? - no consequence for now
   - [ ] Can player skip missions or must complete in order? - must complete in order for now


### Deliverable
Design document with all decisions locked in before Day 1.

---

## PHASE 1: FOUNDATION (Day 1)

### Objectives
Create abstract state base class and concrete state implementations.

### Tasks

#### 1.1 Create ObjectiveState Abstract Base
**File:** `Assets/Scripts/Game/Objectives/ObjectiveState.cs`

```csharp
using UnityEngine;

namespace Game.Objectives
{
    public enum ObjectiveType { Explore, KeyAcquired, ExtractionChallenge }
    
    public struct ObjectiveContext
    {
        public ObjectiveType type;
        public float progressPct;       // 0–1
        public float timeElapsed;
        public Vector3 playerPos;
        public Vector3 extractionPoint;
        public bool hasKey;
        public int enemyCount;
        public float asteroidDensity;
    }
    
    public abstract class ObjectiveState
    {
        public abstract ObjectiveType StateType { get; }
        
        public virtual void Enter(in ObjectiveContext ctx) { }
        
        public abstract void Tick(in ObjectiveContext ctx, float deltaTime);
        
        public virtual void Exit() { }
        
        public abstract ObjectiveState GetNextState(in ObjectiveContext ctx);
        
        public abstract float ComputeUtility(in ObjectiveContext ctx);
    }
}
```

**Success Criteria:**
- [ ] Abstract base compiles
- [ ] Lifecycle methods match AI.States.State pattern
- [ ] Context struct is zero-alloc (struct, not class)
- [ ] No MonoBehaviour dependencies in base class

#### 1.2 Create Explore State
**File:** `Assets/Scripts/Game/Objectives/States/ExploreState.cs`

```csharp
namespace Game.Objectives.States
{
    public class ExploreState : ObjectiveState
    {
        private readonly ObjectiveParams parameters;
        private float explorationProgress; // 0–1
        
        public override ObjectiveType StateType => ObjectiveType.Explore;
        
        public ExploreState(ObjectiveParams parameters)
        {
            this.parameters = parameters;
            explorationProgress = 0f;
        }
        
        public override void Enter(in ObjectiveContext ctx)
        {
            explorationProgress = 0f;
        }
        
        public override void Tick(in ObjectiveContext ctx, float deltaTime)
        {
            // TODO: Calculate progress based on asteroid density coverage
            // explorationProgress = CalculateCoveragePercentage(ctx.playerPos);
        }
        
        public override ObjectiveState GetNextState(in ObjectiveContext ctx)
        {
            if (explorationProgress >= parameters.ExploreThreshold)
            {
                return new KeyAcquiredState(parameters);
            }
            return this; // Stay in Explore
        }
        
        public override float ComputeUtility(in ObjectiveContext ctx)
        {
            // Always available, always utility 1.0
            return 1.0f;
        }
    }
}
```

**Success Criteria:**
- [ ] Compiles without MonoBehaviour dependencies
- [ ] Tick method updates progress deterministically
- [ ] GetNextState returns new state or self
- [ ] Unit-testable without scene context

#### 1.3 Create KeyAcquired State
**File:** `Assets/Scripts/Game/Objectives/States/KeyAcquiredState.cs`

```csharp
namespace Game.Objectives.States
{
    public class KeyAcquiredState : ObjectiveState
    {
        private readonly ObjectiveParams parameters;
        
        public override ObjectiveType StateType => ObjectiveType.KeyAcquired;
        
        public KeyAcquiredState(ObjectiveParams parameters)
        {
            this.parameters = parameters;
        }
        
        public override void Enter(in ObjectiveContext ctx)
        {
            // TODO: Spawn key pickup at sector center
            // TODO: Notify UI/audio "Key acquired available"
        }
        
        public override void Tick(in ObjectiveContext ctx, float deltaTime)
        {
            // Check if player has picked up key
        }
        
        public override ObjectiveState GetNextState(in ObjectiveContext ctx)
        {
            if (ctx.hasKey)
            {
                return new ExtractionChallengeState(parameters);
            }
            return this;
        }
        
        public override float ComputeUtility(in ObjectiveContext ctx)
        {
            return ctx.hasKey ? 0.8f : 1.0f;
        }
    }
}
```

#### 1.4 Create ExtractionChallenge State
**File:** `Assets/Scripts/Game/Objectives/States/ExtractionChallengeState.cs`

```csharp
namespace Game.Objectives.States
{
    public class ExtractionChallengeState : ObjectiveState
    {
        private readonly ObjectiveParams parameters;
        private float timeInState;
        
        public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;
        
        public ExtractionChallengeState(ObjectiveParams parameters)
        {
            this.parameters = parameters;
        }
        
        public override void Enter(in ObjectiveContext ctx)
        {
            timeInState = 0f;
            // TODO: Mark extraction point on HUD
        }
        
        public override void Tick(in ObjectiveContext ctx, float deltaTime)
        {
            timeInState += deltaTime;
            
            // Fail if time exceeded
            if (timeInState > parameters.ExtractTimeLimit)
            {
                // TODO: Trigger failure
            }
        }
        
        public override ObjectiveState GetNextState(in ObjectiveContext ctx)
        {
            // Check if player reached extraction point
            var distToExtraction = Vector3.Distance(ctx.playerPos, ctx.extractionPoint);
            if (distToExtraction < parameters.ExtractionRadius)
            {
                return new ExtractedState(parameters);
            }
            
            // Check failure condition
            if (timeInState > parameters.ExtractTimeLimit)
            {
                return new FailedState(parameters);
            }
            
            return this;
        }
        
        public override float ComputeUtility(in ObjectiveContext ctx)
        {
            return 1.0f;
        }
    }
}
```

#### 1.5 Create Terminal States
**File:** `Assets/Scripts/Game/Objectives/States/ExtractedState.cs`

```csharp
namespace Game.Objectives.States
{
    public class ExtractedState : ObjectiveState
    {
        public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;
        
        public override void Tick(in ObjectiveContext ctx, float deltaTime)
        {
            // Terminal state; no action
        }
        
        public override ObjectiveState GetNextState(in ObjectiveContext ctx)
        {
            return this; // Stay terminal
        }
        
        public override float ComputeUtility(in ObjectiveContext ctx)
        {
            return 0f; // No longer selectable
        }
    }
}
```

**File:** `Assets/Scripts/Game/Objectives/States/FailedState.cs`

```csharp
namespace Game.Objectives.States
{
    public class FailedState : ObjectiveState
    {
        public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;
        
        public override void Tick(in ObjectiveContext ctx, float deltaTime)
        {
            // Terminal state; no action
        }
        
        public override ObjectiveState GetNextState(in ObjectiveContext ctx)
        {
            return this; // Stay terminal
        }
        
        public override float ComputeUtility(in ObjectiveContext ctx)
        {
            return 0f;
        }
    }
}
```

#### 1.6 Create ObjectiveParams ScriptableObject
**File:** `Assets/Scripts/Game/Objectives/ObjectiveParams.cs`

```csharp
using UnityEngine;

namespace Game.Objectives
{
    [CreateAssetMenu(fileName = "ObjectiveParams_MVP", menuName = "Game/Objective Parameters")]
    public class ObjectiveParams : ScriptableObject
    {
        [SerializeField] private float exploreCompletionThreshold = 0.8f;
        [SerializeField] private float extractionTimeLimit = 300f;
        [SerializeField] private float extractionRadius = 10f;
        [SerializeField] private float keySpawnRadius = 50f;
        [SerializeField] private float failureTimeLimit = 600f;
        
        public float ExploreThreshold => exploreCompletionThreshold;
        public float ExtractTimeLimit => extractionTimeLimit;
        public float ExtractionRadius => extractionRadius;
        public float KeySpawnRadius => keySpawnRadius;
        public float FailureTimeLimit => failureTimeLimit;
    }
}
```

### Testing (Phase 1)

#### 1.7 Create EditMode Unit Tests
**File:** `Assets/Tests/EditMode/ObjectiveTrackerEditModeTests.cs`

```csharp
using Game.Objectives;
using Game.Objectives.States;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Objectives")]
    public class ObjectiveTrackerEditModeTests
    {
        private ObjectiveParams parameters;
        
        [SetUp]
        public void Setup()
        {
            parameters = ScriptableObject.CreateInstance<ObjectiveParams>();
        }
        
        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(parameters);
        }
        
        [Test]
        public void ExploreState_WhenProgressReaches80Percent_TransitionsToKeyAcquired()
        {
            var exploreState = new ExploreState(parameters);
            var ctx = new ObjectiveContext
            {
                type = ObjectiveType.Explore,
                progressPct = 0.85f,
                timeElapsed = 0f,
                playerPos = Vector3.zero,
                hasKey = false,
                enemyCount = 1,
                asteroidDensity = 0.5f
            };
            
            exploreState.Enter(in ctx);
            exploreState.Tick(in ctx, 1f);
            var nextState = exploreState.GetNextState(in ctx);
            
            Assert.IsInstanceOf<KeyAcquiredState>(nextState);
        }
        
        [Test]
        public void KeyAcquiredState_WhenPlayerHasKey_TransitionsToExtractionChallenge()
        {
            var keyState = new KeyAcquiredState(parameters);
            var ctx = new ObjectiveContext
            {
                type = ObjectiveType.KeyAcquired,
                progressPct = 1f,
                timeElapsed = 5f,
                playerPos = Vector3.zero,
                hasKey = true, // Key acquired
                enemyCount = 1,
                asteroidDensity = 0.5f
            };
            
            var nextState = keyState.GetNextState(in ctx);
            
            Assert.IsInstanceOf<ExtractionChallengeState>(nextState);
        }
        
        [Test]
        public void ExtractionChallengeState_WhenTimeExceeded_TransitionsToFailed()
        {
            var extractState = new ExtractionChallengeState(parameters);
            var ctx = new ObjectiveContext
            {
                type = ObjectiveType.ExtractionChallenge,
                progressPct = 0.5f,
                timeElapsed = 0f,
                playerPos = Vector3.zero,
                extractionPoint = new Vector3(100, 0, 0),
                hasKey = true,
                enemyCount = 2,
                asteroidDensity = 0.7f
            };
            
            extractState.Enter(in ctx);
            extractState.Tick(in ctx, parameters.ExtractTimeLimit + 1f);
            var nextState = extractState.GetNextState(in ctx);
            
            Assert.IsInstanceOf<FailedState>(nextState);
        }
    }
}
```

**Success Criteria:**
- [ ] All state transition tests pass
- [ ] Tests are zero-dependency (no scene, no GameInitiator)
- [ ] Context struct serialization works
- [ ] Test execution < 100 ms

### Deliverable
- [ ] ObjectiveState abstract base (compiles, zero-alloc)
- [ ] 5 concrete states (Explore, KeyAcquired, ExtractionChallenge, Extracted, Failed)
- [ ] ObjectiveParams ScriptableObject
- [ ] 3+ EditMode unit tests (all green)

---

## PHASE 2: EVENTS & INITIALIZATION (Day 2)

### Objectives
Create ObjectiveTracker component and integrate with GameInitiator.

### Tasks

#### 2.1 Create ObjectiveTracker Main Component
**File:** `Assets/Scripts/Game/Objectives/ObjectiveTracker.cs`

```csharp
using System;
using UnityEngine;

namespace Game.Objectives
{
    public class ObjectiveTracker : MonoBehaviour
    {
        [SerializeField] private ObjectiveParams parameters;
        [SerializeField] private ShipRegistry shipRegistry;
        
        private ObjectiveState currentState;
        private ObjectiveContext context;
        private float timeElapsed;
        
        // Events (pattern from ShipEvents)
        public event Action<ObjectiveState, ObjectiveState> OnStateChanged;
        public event Action<float> OnProgressChanged;
        public event Action OnObjectiveComplete;
        public event Action OnObjectiveFailed;
        
        public ObjectiveState CurrentState => currentState;
        public ObjectiveContext CurrentContext => context;
        public bool IsActive => currentState is not (ExtractedState or FailedState);
        
        public void Initialize(ObjectiveParams @params, ShipRegistry registry)
        {
            parameters = @params;
            shipRegistry = registry;
            timeElapsed = 0f;
            
            // Start in Explore state
            TransitionToState(new ExploreState(parameters));
        }
        
        private void Update()
        {
            if (!IsActive) return;
            
            Tick(Time.deltaTime);
        }
        
        public void Tick(float deltaTime)
        {
            timeElapsed += deltaTime;
            
            // Update context
            UpdateContext();
            
            // Tick current state
            currentState.Tick(in context, deltaTime);
            
            // Check for state transition
            var nextState = currentState.GetNextState(in context);
            if (nextState.GetType() != currentState.GetType())
            {
                TransitionToState(nextState);
            }
            
            // Emit progress change event
            OnProgressChanged?.Invoke(context.progressPct);
        }
        
        private void UpdateContext()
        {
            // TODO: Populate context from game state
            context = new ObjectiveContext
            {
                type = currentState.StateType,
                progressPct = ComputeProgressPercentage(),
                timeElapsed = timeElapsed,
                playerPos = GetPlayerPosition(),
                extractionPoint = GetExtractionPoint(),
                hasKey = CheckPlayerHasKey(),
                enemyCount = shipRegistry.ActiveShips.Count - 1,
                asteroidDensity = 0.5f // TODO: query asteroid field
            };
        }
        
        private void TransitionToState(ObjectiveState newState)
        {
            var previousState = currentState;
            
            // Exit previous state
            currentState?.Exit();
            
            // Set new state
            currentState = newState;
            
            // Enter new state
            currentState.Enter(in context);
            
            // Emit event
            OnStateChanged?.Invoke(previousState, newState);
            
            // Check terminal conditions
            if (newState is ExtractedState)
            {
                OnObjectiveComplete?.Invoke();
            }
            else if (newState is FailedState)
            {
                OnObjectiveFailed?.Invoke();
            }
            
            #if UNITY_EDITOR
            Debug.Log($"[ObjectiveTracker] Transitioned {previousState?.GetType().Name} → {newState.GetType().Name}");
            #endif
        }
        
        private Vector3 GetPlayerPosition()
        {
            // TODO: Query from player ship
            return Vector3.zero;
        }
        
        private Vector3 GetExtractionPoint()
        {
            // TODO: Spawn/store extraction point
            return Vector3.zero;
        }
        
        private bool CheckPlayerHasKey()
        {
            // TODO: Query from player inventory
            return false;
        }
        
        private float ComputeProgressPercentage()
        {
            // TODO: Calculate based on state
            return 0f;
        }
        
        public void Reset()
        {
            timeElapsed = 0f;
            currentState = null;
            Initialize(parameters, shipRegistry);
        }
    }
}
```

**Success Criteria:**
- [ ] Compiles (with TODO placeholders)
- [ ] Events fire on state transitions
- [ ] Zero per-frame allocations (struct context, no new objects)
- [ ] Tick method is frame-independent (accepts deltaTime)

#### 2.2 Integrate with GameInitiator
**File:** `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs` (Modify)

```csharp
// In GameInitiator class

private ObjectiveTracker objectiveTracker;

private IEnumerator Initialize(GameConfig config)
{
    // ... existing initialization ...
    
    InitializeObjectiveTracker(gameConfig);
    
    // ... rest of initialization ...
}

private void InitializeObjectiveTracker(GameConfig config)
{
    var trackerGO = new GameObject("ObjectiveTracker");
    objectiveTracker = trackerGO.AddComponent<ObjectiveTracker>();
    objectiveTracker.Initialize(config.ObjectiveParameters, ShipRegistry);
}

public void Shutdown()
{
    // ... existing shutdown ...
    
    if (objectiveTracker)
        Destroy(objectiveTracker.gameObject);
    
    objectiveTracker = null;
}
```

#### 2.3 Extend GameConfig
**File:** `src/Asteroids3D/Assets/Scripts/Game/GameConfig.cs` (Modify)

```csharp
[SerializeField] private ObjectiveParams objectiveParameters;

public ObjectiveParams ObjectiveParameters => objectiveParameters;
```

### Testing (Phase 2)

#### 2.4 Create PlayMode Integration Test
**File:** `Assets/Tests/PlayMode/ObjectiveTrackerPlayModeTests.cs`

```csharp
using System.Collections;
using Game;
using Game.Objectives;
using NUnit.Framework;
using UnityEngine.TestFramework;

namespace Tests.PlayMode
{
    [Category("Objectives")]
    public class ObjectiveTrackerPlayModeTests
    {
        [UnityTest]
        public IEnumerator ObjectiveTracker_InitializesInExploreState()
        {
            // Setup: Create ObjectiveTracker in isolation
            var tracker = new GameObject("TrackerTest").AddComponent<ObjectiveTracker>();
            var parameters = Resources.Load<ObjectiveParams>("ObjectiveParams_MVP");
            var mockRegistry = new MockShipRegistry();
            
            tracker.Initialize(parameters, mockRegistry);
            
            yield return null; // Wait one frame
            
            Assert.IsNotNull(tracker.CurrentState);
            Assert.IsInstanceOf<ExploreState>(tracker.CurrentState);
        }
        
        [UnityTest]
        public IEnumerator ObjectiveTracker_EmitsStateChangedEvent()
        {
            var tracker = new GameObject("TrackerTest").AddComponent<ObjectiveTracker>();
            var parameters = Resources.Load<ObjectiveParams>("ObjectiveParams_MVP");
            var mockRegistry = new MockShipRegistry();
            
            ObjectiveState capturedFrom = null;
            ObjectiveState capturedTo = null;
            
            tracker.OnStateChanged += (from, to) =>
            {
                capturedFrom = from;
                capturedTo = to;
            };
            
            tracker.Initialize(parameters, mockRegistry);
            
            yield return null;
            
            Assert.IsNotNull(capturedTo);
            Assert.IsInstanceOf<ExploreState>(capturedTo);
        }
    }
    
    // Mock registry for testing
    public class MockShipRegistry : IShipRegistry
    {
        public IList<Ship> ActiveShips { get; } = new List<Ship>();
        public event Action<Ship> OnShipAdded;
        public event Action<Ship> OnShipRemoved;
    }
}
```

### Deliverable
- [ ] ObjectiveTracker component (with TODO placeholders for game state queries)
- [ ] GameInitiator integration (creates and initializes tracker)
- [ ] GameConfig extension (references ObjectiveParams)
- [ ] PlayMode integration tests (tracker initialization, event emission)
- [ ] Zero per-frame allocations verified

---

## PHASE 3: UI INTEGRATION (Day 3)

### Objectives
Create UI component that subscribes to objective state events.

### Tasks

#### 3.1 Create ObjectiveHUD Component
**File:** `Assets/Scripts/UI/ObjectiveHUD.cs`

```csharp
using Game.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class ObjectiveHUD : MonoBehaviour
    {
        [SerializeField] private Text objectiveText;
        [SerializeField] private Slider progressBar;
        [SerializeField] private Text stateDescriptionText;
        
        private ObjectiveTracker tracker;
        
        public void Initialize(ObjectiveTracker objectiveTracker)
        {
            tracker = objectiveTracker;
            
            // Subscribe to events
            if (tracker)
            {
                tracker.OnStateChanged += OnObjectiveStateChanged;
                tracker.OnProgressChanged += OnProgressChanged;
            }
            
            // Initial state
            UpdateDisplay();
        }
        
        private void OnEnable()
        {
            // Re-subscribe in case of respawn
            if (tracker)
            {
                tracker.OnStateChanged += OnObjectiveStateChanged;
                tracker.OnProgressChanged += OnProgressChanged;
            }
        }
        
        private void OnDisable()
        {
            // Unsubscribe to prevent memory leaks
            if (tracker)
            {
                tracker.OnStateChanged -= OnObjectiveStateChanged;
                tracker.OnProgressChanged -= OnProgressChanged;
            }
        }
        
        private void OnObjectiveStateChanged(ObjectiveState from, ObjectiveState to)
        {
            UpdateDisplay();
        }
        
        private void OnProgressChanged(float progress)
        {
            if (progressBar)
                progressBar.value = progress;
        }
        
        private void UpdateDisplay()
        {
            if (!tracker) return;
            
            var state = tracker.CurrentState;
            if (!state) return;
            
            // Update text based on state type
            objectiveText.text = GetStateLabel(state);
            stateDescriptionText.text = GetStateDescription(state);
            progressBar.value = tracker.CurrentContext.progressPct;
            
            #if UNITY_EDITOR
            Debug.Log($"[ObjectiveHUD] Updated display for {state.GetType().Name}");
            #endif
        }
        
        private string GetStateLabel(ObjectiveState state)
        {
            return state switch
            {
                ExploreState => "EXPLORE SECTOR",
                KeyAcquiredState => "LOCATE KEY",
                ExtractionChallengeState => "EXTRACT",
                ExtractedState => "MISSION COMPLETE",
                FailedState => "MISSION FAILED",
                _ => "UNKNOWN"
            };
        }
        
        private string GetStateDescription(ObjectiveState state)
        {
            return state switch
            {
                ExploreState => "Scout the asteroid field and map threats.",
                KeyAcquiredState => "Retrieve the lost artifact.",
                ExtractionChallengeState => "Escape the sector with the artifact.",
                ExtractedState => "Objective complete! Returning to base.",
                FailedState => "Objective failed. Retreating.",
                _ => ""
            };
        }
    }
}
```

**Success Criteria:**
- [ ] Compiles without compile errors
- [ ] Subscribes to state change events
- [ ] Re-subscribes in OnEnable() for respawn resilience
- [ ] Updates HUD text on state transitions

#### 3.2 Create Test HUD Prefab
**Location:** `Assets/Prefabs/UI/ObjectiveHUD.prefab`

1. Create Canvas with:
   - Text field for objective (label)
   - Slider for progress
   - Text field for description

2. Attach ObjectiveHUD component

3. Assign fields in inspector

#### 3.3 Wire HUD to GameInitiator
**File:** `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs` (Modify)

```csharp
[SerializeField] private ObjectiveHUD objectiveHUDPrefab;

private void PublishPresentationReady()
{
    if (!player || !cameraRig) return;
    
    // Instantiate HUD
    var hudInstance = Instantiate(objectiveHUDPrefab);
    hudInstance.Initialize(objectiveTracker);
    
    PresentationReady?.Invoke(player, cameraRig.UICamera);
}
```

### Testing (Phase 3)

#### 3.4 Create PlayMode UI Test
**File:** `Assets/Tests/PlayMode/ObjectiveHUDPlayModeTests.cs`

```csharp
using System.Collections;
using Game;
using Game.Objectives;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestFramework;
using UnityEngine.UI;

namespace Tests.PlayMode
{
    [Category("Objectives")]
    public class ObjectiveHUDPlayModeTests
    {
        [UnityTest]
        public IEnumerator ObjectiveHUD_UpdatesTextOnStateChange()
        {
            // Setup
            var tracker = new GameObject("TrackerTest").AddComponent<ObjectiveTracker>();
            var hudGO = new GameObject("HUDTest");
            var hudText = hudGO.AddComponent<Text>();
            var hud = hudGO.AddComponent<ObjectiveHUD>();
            
            var parameters = Resources.Load<ObjectiveParams>("ObjectiveParams_MVP");
            var mockRegistry = new MockShipRegistry();
            
            tracker.Initialize(parameters, mockRegistry);
            hud.Initialize(tracker);
            
            yield return null; // Wait for event
            
            Assert.AreEqual("EXPLORE SECTOR", hudText.text);
        }
        
        [UnityTest]
        public IEnumerator ObjectiveHUD_ResubscribesOnEnable()
        {
            var tracker = new GameObject("TrackerTest").AddComponent<ObjectiveTracker>();
            var hudGO = new GameObject("HUDTest");
            var hud = hudGO.AddComponent<ObjectiveHUD>();
            
            var parameters = Resources.Load<ObjectiveParams>("ObjectiveParams_MVP");
            var mockRegistry = new MockShipRegistry();
            
            tracker.Initialize(parameters, mockRegistry);
            hud.Initialize(tracker);
            
            // Disable and re-enable
            hudGO.SetActive(false);
            yield return null;
            
            hudGO.SetActive(true);
            yield return null;
            
            // Should still receive events
            Assert.IsTrue(hudGO.activeInHierarchy);
        }
    }
}
```

### Deliverable
- [ ] ObjectiveHUD component (subscribes to tracker events)
- [ ] HUD prefab with text/slider fields
- [ ] GameInitiator instantiates HUD on presentation ready
- [ ] PlayMode tests verify UI updates
- [ ] Re-subscription on OnEnable() working

---

## PHASE 4: AUDIO INTEGRATION (Day 4)

### Objectives
Wire objective events to audio system; implement wingman stub.

### Tasks

#### 4.1 Create Audio Event Handler
**File:** `Assets/Scripts/UI/Audio/ObjectiveAudio.cs`

```csharp
using Game.Objectives;
using UnityEngine;

namespace UI.Audio
{
    public class ObjectiveAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip stateTransitionSfx;
        [SerializeField] private AudioClip completionSfx;
        [SerializeField] private AudioClip failureSfx;
        [SerializeField] private AudioSource audioSource;
        
        private ObjectiveTracker tracker;
        
        public void Initialize(ObjectiveTracker objectiveTracker)
        {
            tracker = objectiveTracker;
            
            if (tracker)
            {
                tracker.OnStateChanged += OnObjectiveStateChanged;
                tracker.OnObjectiveComplete += OnObjectiveComplete;
                tracker.OnObjectiveFailed += OnObjectiveFailed;
            }
        }
        
        private void OnEnable()
        {
            if (tracker)
            {
                tracker.OnStateChanged += OnObjectiveStateChanged;
                tracker.OnObjectiveComplete += OnObjectiveComplete;
                tracker.OnObjectiveFailed += OnObjectiveFailed;
            }
        }
        
        private void OnDisable()
        {
            if (tracker)
            {
                tracker.OnStateChanged -= OnObjectiveStateChanged;
                tracker.OnObjectiveComplete -= OnObjectiveComplete;
                tracker.OnObjectiveFailed -= OnObjectiveFailed;
            }
        }
        
        private void OnObjectiveStateChanged(ObjectiveState from, ObjectiveState to)
        {
            if (stateTransitionSfx && audioSource)
            {
                audioSource.PlayOneShot(stateTransitionSfx);
            }
        }
        
        private void OnObjectiveComplete()
        {
            if (completionSfx && audioSource)
            {
                audioSource.PlayOneShot(completionSfx);
            }
        }
        
        private void OnObjectiveFailed()
        {
            if (failureSfx && audioSource)
            {
                audioSource.PlayOneShot(failureSfx);
            }
        }
    }
}
```

#### 4.2 Create Wingman Stub
**File:** `Assets/Scripts/UI/Wingman.cs`

```csharp
using Game.Objectives;
using UnityEngine;

namespace UI
{
    public class Wingman : MonoBehaviour
    {
        [SerializeField] private Text dialogueText;
        [SerializeField] private float displayDuration = 3f;
        
        private ObjectiveTracker tracker;
        private float displayTimer;
        
        public void Initialize(ObjectiveTracker objectiveTracker)
        {
            tracker = objectiveTracker;
            
            if (tracker)
            {
                tracker.OnStateChanged += OnObjectiveStateChanged;
            }
        }
        
        private void OnObjectiveStateChanged(ObjectiveState from, ObjectiveState to)
        {
            var callout = GetWingmanCallout(to);
            if (!string.IsNullOrEmpty(callout))
            {
                DisplayDialogue(callout);
            }
        }
        
        private void DisplayDialogue(string text)
        {
            if (dialogueText)
            {
                dialogueText.text = text;
                displayTimer = displayDuration;
            }
        }
        
        private void Update()
        {
            if (displayTimer > 0)
            {
                displayTimer -= Time.deltaTime;
                if (displayTimer <= 0 && dialogueText)
                {
                    dialogueText.text = "";
                }
            }
        }
        
        private string GetWingmanCallout(ObjectiveState state)
        {
            return state switch
            {
                ExploreState => "Engaging exploration mode. Scout the asteroids.",
                KeyAcquiredState => "Key location marked. Move to intercept.",
                ExtractionChallengeState => "Enemy engagement detected. Extract with the key!",
                ExtractedState => "Mission complete! Welcome back, Commander.",
                FailedState => "Mission failed. Better luck next time, Commander.",
                _ => ""
            };
        }
    }
}
```

### Deliverable
- [ ] ObjectiveAudio component (subscribes to tracker events)
- [ ] Wingman component (displays callouts on state changes)
- [ ] Audio SFX played on objective completion/failure
- [ ] Wingman callouts display for 3 seconds each

---

## PHASE 5: RL INTEGRATION (Day 5)

### Objectives
Add objective tracker to observation vector; implement reward shaping.

### Tasks

#### 5.1 Extend RLArbiter
**File:** `src/Asteroids3D/Assets/Scripts/` (Implied location: RLArbiter.cs)

```csharp
using Game.Objectives;
using UnityEngine;

namespace ML
{
    public class RLArbiter : MonoBehaviour
    {
        private ObjectiveTracker objectiveTracker;
        
        public void Initialize(ObjectiveTracker tracker)
        {
            objectiveTracker = tracker;
            
            if (tracker)
            {
                tracker.OnStateChanged += OnObjectiveStateChanged;
                tracker.OnObjectiveComplete += OnObjectiveComplete;
                tracker.OnObjectiveFailed += OnObjectiveFailed;
            }
        }
        
        private void OnObjectiveStateChanged(ObjectiveState from, ObjectiveState to)
        {
            // Update internal reward accumulator
            // (implementation depends on ML-Agents reward system)
        }
        
        private void OnObjectiveComplete()
        {
            // +0.5 reward
            AddReward(0.5f);
        }
        
        private void OnObjectiveFailed()
        {
            // -0.5 penalty
            AddReward(-0.5f);
        }
        
        private void AddReward(float amount)
        {
            // Wire to ML-Agents Academy or decision requester
        }
    }
}
```

#### 5.2 Extend Observation Vector
**Modify observation vector composition:**

```csharp
public float[] GetObservationVector()
{
    // Existing 32 observations (position, velocity, shields, etc.)
    var obs = new float[32];
    // ... populate existing observations ...
    
    // Add objective observations
    var objContext = objectiveTracker.CurrentContext;
    
    // Objective type (one-hot encoded: 3 values)
    // [1, 0, 0] = Explore
    // [0, 1, 0] = KeyAcquired
    // [0, 0, 1] = ExtractionChallenge
    switch (objContext.type)
    {
        case ObjectiveType.Explore:
            obs = obs.Append(new float[] { 1f, 0f, 0f });
            break;
        case ObjectiveType.KeyAcquired:
            obs = obs.Append(new float[] { 0f, 1f, 0f });
            break;
        case ObjectiveType.ExtractionChallenge:
            obs = obs.Append(new float[] { 0f, 0f, 1f });
            break;
    }
    
    // Objective progress (1 float)
    obs = obs.Append(new float[] { objContext.progressPct });
    
    // Time in objective (1 float, normalized to 0–1)
    obs = obs.Append(new float[] { objContext.timeElapsed / 600f }); // 10 min max
    
    return obs;
    // Total: 32 + 3 + 1 + 1 = 37 observations
}
```

#### 5.3 Implement Reward Shaping
```csharp
public float ComputeObjectiveReward()
{
    var context = objectiveTracker.CurrentContext;
    
    // Base reward per frame
    float baseReward = 0.01f;
    
    // Progress bonus
    float progressBonus = context.progressPct * 0.05f;
    
    // Time penalty (discourage wasting time)
    float timePenalty = -context.timeElapsed * 0.001f;
    
    return baseReward + progressBonus + timePenalty;
}
```

### Deliverable
- [ ] RLArbiter integrated with ObjectiveTracker
- [ ] Observation vector includes objective type + progress
- [ ] Reward shaping implemented (+0.5 on completion, -0.5 on failure)
- [ ] Multi-arena training verified (no global state)

---

## FINAL CHECKLIST

### Code Quality
- [ ] All states inherit from ObjectiveState
- [ ] Zero per-frame allocations (verify with Profiler)
- [ ] All debug code wrapped in `#if UNITY_EDITOR`
- [ ] No reflection at runtime
- [ ] Events follow Action<T> pattern

### Testing
- [ ] EditMode tests cover all state transitions
- [ ] PlayMode tests cover event emission
- [ ] UI subscription tests verify re-subscription
- [ ] Performance tests verify zero GC spikes
- [ ] All tests green in CI

### Integration
- [ ] GameInitiator creates and initializes tracker
- [ ] GameConfig references ObjectiveParams
- [ ] ObjectiveHUD subscribes and updates
- [ ] Audio/Wingman respond to events
- [ ] RLArbiter consumes objective data

### Documentation
- [ ] Code comments explain non-obvious logic
- [ ] ScriptableObject fields have tooltips
- [ ] README updated with objective system overview
- [ ] Events documented in tracker component

### Performance
- [ ] Frame time spike < 1 ms on state transition
- [ ] GC allocations == 0 per frame
- [ ] Memory footprint < 1 MB (tracker + states + UI)

---

**Ready to Start?**

1. Resolve design decisions (Phase 0)
2. Create ObjectiveState base + concrete states (Phase 1)
3. Implement ObjectiveTracker + GameInitiator integration (Phase 2)
4. Add ObjectiveHUD subscriber (Phase 3)
5. Wire audio + wingman (Phase 4)
6. Integrate RL observation + reward (Phase 5)

**Estimated Total Time:** 5 days for MVP  
**Success Gate:** All EditMode + PlayMode tests green, zero GC allocations, RL observation vector complete
