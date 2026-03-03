# Objective Tracker State Machine — Research Package Index

**Research Date:** March 3, 2026  
**Status:** Initial research complete; ready for design review and implementation  
**Total Documentation:** 4 comprehensive guides (70+ KB)

---

## 📋 DOCUMENT GUIDE

### 1. **OBJECTIVE_TRACKER_RESEARCH.md** (Primary Research Report)
**Size:** 26 KB | **Duration to Read:** 30–45 min

**Contains:**
- Executive summary of findings
- 8 major research sections (game loop, state machines, events, etc.)
- Key findings with source paths
- Important constraints & performance requirements
- Open questions (8 design decisions needed)
- Recommended reading list
- Initial architecture proposal
- Handoff summary for implementation

**Best For:** Understanding the full context and design landscape

**Key Sections:**
1. Game loop architecture (MVP single-encounter model)
2. Proven state machine patterns (emulate AI.States.State)
3. Event-driven patterns (in progress, blocking RL pipeline)
4. Mission/objective structure (gap: not yet designed)
5. Dependency injection & testing patterns
6. Performance constraints (zero allocations, < 1 ms events)
7. UI/audio integration pathways
8. ML-Agents/RL integration needs

---

### 2. **OBJECTIVE_TRACKER_CODEBASE_MAP.md** (Quick Reference)
**Size:** 13 KB | **Duration to Read:** 15–20 min

**Contains:**
- 11 sections mapping existing patterns to new code
- File locations and import paths
- Code snippets for each pattern
- Test examples
- Configuration patterns
- ML-Agents integration points
- Quick checklist for implementation

**Best For:** Looking up specific patterns while coding

**Key Sections:**
1. State machine patterns (template to emulate)
2. Game initialization & integration points
3. Event-driven patterns (model to follow)
4. Ship & damage systems (context for objectives)
5. Respawn & lifecycle management
6. Performance & testing patterns
7. Testing infrastructure
8. Configuration & tuning patterns
9. ML-Agents/RL integration points
10. Quick implementation checklist

---

### 3. **OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md** (Detailed Task Breakdown)
**Size:** 38 KB | **Duration to Read:** 60–90 min | **Action:** Start here to code

**Contains:**
- 5-day implementation roadmap (MVP scope)
- Phase 0: Design review (resolve open questions)
- Phase 1: Foundation (state base + 5 concrete states, 1 day)
- Phase 2: Events & initialization (ObjectiveTracker, 1 day)
- Phase 3: UI integration (ObjectiveHUD, 1 day)
- Phase 4: Audio integration (sfx + wingman stub, 1 day)
- Phase 5: RL integration (observation + reward, 1 day)
- Complete code snippets for every file
- Unit tests for each phase
- Success criteria for each deliverable

**Best For:** Step-by-step implementation with code ready to use

**Key Sections:**
1. **Phase 0: Design Review** (1.1 resolves 6 open design decisions)
2. **Phase 1: Foundation** (1.1–1.7 creates states + tests)
3. **Phase 2: Events & Initialization** (2.1–2.4 wires to GameInitiator)
4. **Phase 3: UI Integration** (3.1–3.4 creates HUD + tests)
5. **Phase 4: Audio Integration** (4.1–4.2 audio + wingman stub)
6. **Phase 5: RL Integration** (5.1–5.3 observation vector + reward)
7. **Final Checklist** (code quality, testing, integration, docs, performance)

---

### 4. **OBJECTIVE_TRACKER_RESEARCH.md Summary** (Clipboard-Ready)
**Location:** Clipboard (run `copy_to_clipboard` to paste)

**Contains:**
- 1-page executive summary
- What we know ✅ (5 items)
- What we don't know ⚠️ (6 design decisions)
- Proposed architecture pattern (code sketch)
- Implementation roadmap (5 days)
- Key integration points
- Source file references
- Recommended next steps

**Best For:** Quick briefing or sharing with team

---

## 🎯 HOW TO USE THIS PACKAGE

### For a **Design Review Meeting**
1. Read OBJECTIVE_TRACKER_RESEARCH.md (executive summary section)
2. Review the 6 open questions (Section "OPEN QUESTIONS / UNKNOWNS")
3. Print the Clipboard summary for distribution
4. Discuss design decisions before Phase 0 starts

### For **Starting Implementation**
1. Complete Phase 0 design review (resolve open questions)
2. Open OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md
3. Follow Phase 1 (Day 1) step-by-step
4. Use OBJECTIVE_TRACKER_CODEBASE_MAP.md as reference
5. Run tests after each phase
6. Check final checklist at end

### For **Understanding Patterns**
1. Read OBJECTIVE_TRACKER_RESEARCH.md Sections 2–7 (patterns)
2. Cross-reference with OBJECTIVE_TRACKER_CODEBASE_MAP.md (file locations)
3. Review code snippets in OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md

### For **Quick Lookups**
1. Use OBJECTIVE_TRACKER_CODEBASE_MAP.md for file paths
2. Use OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md for code snippets
3. Refer to OBJECTIVE_TRACKER_RESEARCH.md for architecture decisions

---

## 📊 KEY FINDINGS SUMMARY

### ✅ What's Already Built (Proven Patterns)
- Finite state machine with Enter/Tick/Exit lifecycle
- Context struct pattern for zero-alloc state passing
- Event-driven UI subscription with re-subscription on reset
- Dependency injection for loose coupling
- ScriptableObject configuration pattern
- Test infrastructure (EditMode + PlayMode)

### ⚠️ What's Missing (Design Decisions)
1. **Extraction mechanics** — How does player escape?
2. **Failure conditions** — What triggers objective failure?
3. **Wingman scope** — Full voice system or stub?
4. **Multi-objective handling** — Parallel, nested, or sequential?
5. **Retry logic** — Same encounter or full reset?
6. **RL reward contract** — What metric drives +0.01/frame?

### 🚀 Implementation Readiness
- **Architecture:** Ready (emulate AI.States.State pattern)
- **Integration Points:** Ready (GameInitiator, GameConfig, ShipRegistry)
- **Testing:** Ready (EditMode + PlayMode templates prepared)
- **Performance:** Ready (context struct pattern, zero-alloc approach)
- **RL Pipeline:** Blocking (waiting for ShipEvents, GameStateEventsFacade)

---

## 📚 SOURCE REFERENCES

### Game Architecture
- `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs` — Main init orchestrator
- `src/Asteroids3D/Assets/Scripts/Game/GameConfig.cs` — Config object pattern
- `doc/Feature_Plans/AI_StateSystem_Refactor.md` — State machine spec (template)

### Event Patterns
- `OBSIDIAN_SCOUT_REPORT.md` — Event-driven architecture guide
- `src/Asteroids3D/Assets/Scripts/Editor/Tests/EditMode/EventDrivenRefactorEditModeTests.cs` — Event test patterns

### Performance & Testing
- `doc/Feature_Plans/AI_Performance_Optimization.md` — Performance constraints
- `doc/Feature_Plans/Testing_Plan.md` — Test modalities & infrastructure
- `doc/Feature_Plans/General_Optimizations.md` — Editor-gating, pooling

### RL Integration
- `doc/Feature_Plans/RL_Implementation_Plan.md` § 9 — Blocking dependencies
- `doc/Feature_Plans/Behavior_Upgrades.md` — Reward shaping patterns

### Original Task
- `.ralph/objective-tracker-state-machine.md` — MVP scope & state definitions

---

## 🔄 WORKFLOW RECOMMENDATION

### Week 1: Design & Planning
- **Day 1:** Team design review (resolve 6 open questions from Phase 0)
- **Day 2:** Finalize ObjectiveParams tunables & save as ScriptableObject
- **Day 3:** Prepare test fixtures (MockShipRegistry, test scenes)

### Week 2: Implementation
- **Day 1 (Phase 1):** ObjectiveState base + 5 concrete states
- **Day 2 (Phase 2):** ObjectiveTracker + GameInitiator integration
- **Day 3 (Phase 3):** ObjectiveHUD + UI subscription
- **Day 4 (Phase 4):** Audio + Wingman stub
- **Day 5 (Phase 5):** RL integration + observation vector

### Week 3: Testing & Polish
- **Day 1:** Run full test suite (EditMode + PlayMode)
- **Day 2:** Performance profiling (verify zero GC)
- **Day 3:** Integration testing (multi-arena training setup)
- **Day 4:** RL training first trial
- **Day 5:** Documentation & handoff

---

## ✅ SUCCESS CRITERIA

### Code Quality
- [ ] All states inherit from ObjectiveState abstract base
- [ ] Zero per-frame allocations (context struct only)
- [ ] All debug code wrapped in `#if UNITY_EDITOR`
- [ ] No reflection at runtime
- [ ] Events follow Action<T> delegate pattern

### Testing
- [ ] EditMode tests cover all state transitions (unit-testable without scene)
- [ ] PlayMode tests cover event emission & UI updates
- [ ] UI re-subscription tests verify respawn resilience
- [ ] Performance tests confirm zero GC spikes
- [ ] All tests green in CI before merge

### Integration
- [ ] GameInitiator creates & initializes ObjectiveTracker
- [ ] GameConfig extended with ObjectiveParams reference
- [ ] ObjectiveHUD subscribes to state changes
- [ ] Audio system responds to objective events
- [ ] Wingman displays callouts on state transitions
- [ ] RLArbiter consumes objective observation & reward

### Performance
- [ ] Frame time spike < 1 ms on state transition
- [ ] GC allocations == 0 per frame (profiler verified)
- [ ] Memory footprint < 1 MB total
- [ ] Multi-arena training achieves ≥200 FPS headless

### Documentation
- [ ] Code comments explain non-obvious logic
- [ ] All ScriptableObject fields have tooltips
- [ ] README includes objective system overview
- [ ] Events documented with parameter descriptions

---

## 💡 KEY DECISIONS & RATIONALE

### Why Emulate AI.States.State?
- ✅ Battle-tested pattern (AI refactor completed Jan 2025)
- ✅ Zero allocations (context struct, no reflection)
- ✅ Clean lifecycle (Enter/Tick/Exit)
- ✅ Already understood by team

### Why Event-Driven Instead of Polling?
- ✅ UI components subscribe (loose coupling)
- ✅ Audio system reacts asynchronously
- ✅ RL observation/reward tightly integrated
- ✅ Respawn-safe re-subscription pattern
- ✅ Scales to multi-arena training

### Why Context Struct Instead of Query Methods?
- ✅ Single struct computed once per frame
- ✅ Passed as `in` parameter (zero-copy)
- ✅ Deterministic (all data in one place)
- ✅ Testable without scene context
- ✅ Zero allocations

### Why Phase 0 Design Review?
- ✅ 6 open questions must be locked before coding
- ✅ Affects state machine logic (extraction trigger, failure conditions)
- ✅ Impacts RL reward shaping (which metric = progress)
- ✅ Prevents rework later

---

## 🚨 RISKS & MITIGATIONS

| Risk | Mitigation |
|------|-----------|
| **State machine bloat** | Use ObjectiveParams ScriptableObject for tunables, not enum explosion |
| **Event subscription bugs** | Implement re-subscription tests (like ShipChildComponentStatePlayModeTests) |
| **RL observation inconsistency** | Unit test observation vector serialization before training |
| **Extraction mechanics unclear** | Start with simple "reach edge of arena" in Phase 0, extend later |
| **Multi-arena crosstalk** | Arena-scoped objective trackers (not global), verified in playtest |
| **GC spikes on transitions** | Context struct pattern guarantees zero allocations; profile before/after |

---

## 📞 QUESTIONS?

### For Architecture Questions
→ See OBJECTIVE_TRACKER_RESEARCH.md Sections 2–5 (patterns & constraints)

### For Implementation Questions
→ See OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md (detailed code with explanations)

### For File Locations & Quick Lookups
→ See OBJECTIVE_TRACKER_CODEBASE_MAP.md (index of existing patterns)

### For Design Decisions
→ See OBJECTIVE_TRACKER_RESEARCH.md Section "OPEN QUESTIONS / UNKNOWNS"

---

## 🎓 LEARNING PATH

**If new to the codebase:**
1. Read OBJECTIVE_TRACKER_RESEARCH.md (full context)
2. Review `doc/Feature_Plans/AI_StateSystem_Refactor.md` (state pattern)
3. Study `src/Asteroids3D/Assets/Scripts/AI/States/State.cs` (code template)
4. Review OBJECTIVE_TRACKER_CODEBASE_MAP.md (where things are)
5. Start Phase 1 with OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md

**If familiar with the codebase:**
1. Skim OBJECTIVE_TRACKER_RESEARCH.md sections 2–3 (patterns you know)
2. Focus on Section 4 (mission/objective structure)
3. Jump to OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md Phase 1
4. Use OBJECTIVE_TRACKER_CODEBASE_MAP.md for quick lookups

**If leading the implementation:**
1. Run a design review with OBJECTIVE_TRACKER_RESEARCH.md (open questions)
2. Create a timeline using OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md (5 days)
3. Assign team members to phases
4. Use OBJECTIVE_TRACKER_CODEBASE_MAP.md for tech lead oversight
5. Check off final checklist items weekly

---

## 📦 DELIVERABLES CHECKLIST

### Documentation Provided
- [x] OBJECTIVE_TRACKER_RESEARCH.md (26 KB, full research)
- [x] OBJECTIVE_TRACKER_CODEBASE_MAP.md (13 KB, quick reference)
- [x] OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md (38 KB, step-by-step)
- [x] OBJECTIVE_TRACKER_INDEX.md (this file, navigation guide)

### Code Templates Included
- [x] ObjectiveState abstract base (pattern)
- [x] 5 concrete state examples (Explore, KeyAcquired, ExtractionChallenge, Extracted, Failed)
- [x] ObjectiveTracker main component (events, lifecycle)
- [x] ObjectiveHUD UI subscriber (re-subscription pattern)
- [x] ObjectiveAudio event handler (audio integration)
- [x] Wingman stub (callout system)
- [x] RLArbiter integration (observation + reward)

### Test Templates Included
- [x] EditMode unit tests (state transitions)
- [x] PlayMode integration tests (initialization, events)
- [x] UI subscription tests (OnEnable re-subscription)
- [x] Audio tests (event routing)

### Configuration Templates Included
- [x] ObjectiveParams ScriptableObject (tunables)
- [x] GameConfig extension (references)
- [x] HUD prefab structure (Canvas + UI components)

---

**Ready to begin? Start with Phase 0 of OBJECTIVE_TRACKER_IMPLEMENTATION_PLAN.md**

---

*End of Index*  
*For questions or clarifications, refer to the appropriate document above.*
