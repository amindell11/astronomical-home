# Editor Assembly Split — Completion

Finish what PR #115 started: PR #115 created the editor-only `Game.Core.Editor`
assembly and moved `Assets/Scripts/Editor/` into it, but an audit found **16
nested `Editor/` folders elsewhere under `Assets/Scripts/`** whose contents
still compile into the runtime `Game.Core` assembly behind `#if UNITY_EDITOR`
guards. The goal is that no editor-only *file* lives in the runtime assembly,
so a missed guard is a compile error instead of a broken player build.

## Inventory (audited 2026-07-11)

**A. Standalone inspectors/drawers — movable as-is (PR-A):**

| File | Type |
|------|------|
| `AI/Editor/SerializeReferenceDrawers.cs` | `[CustomPropertyDrawer]` ×2 (GoalStrategy, UtilityFactor) |
| `AI/Editor/StateProfile.Editor.cs` | `[CustomEditor(StateProfile)]` (standalone class despite filename) |
| `AI/Navigation/MPC/Editor/Settings.Editor.cs` | `[CustomEditor(MpcSettings)]` |
| `AI/Strategy/Editor/UtilityWeightDrawer.Editor.cs` | `[CustomPropertyDrawer(StateWeight)]` |
| `Game/Sectors/Editor/SectorEditor.cs` | `[CustomEditor(Sector)]` |

These only reference runtime types from the outside (SerializedProperty and
internal members — covered by the existing
`InternalsVisibleTo("Game.Core.Editor")`). Move = relocate file + `.meta`
(GUID preserved) into `Assets/Scripts/Editor/Inspectors/`.

**B. Partial-class editor files — need conversion (PR-B, per domain):**
25 files: the 23 `partial class X` `.Editor.cs` files (AICommander, Brain,
GoalRunner, Gunner, Scout, NavFieldService, Cost, Navigator, UtilityBuilder,
Field, UpdatingAsteroidField, CameraFollow, Missile, LockOnSensor, LaserGun,
Missiles, Sector, EncounterSequenceModule, PlayerCommander, PlayerRig,
DamageController, MovementController, AICommander.Observation) plus
`AI/Editor/AIState.cs` (partial class) and
`Asteroids/Editor/AsteroidController.editor.cs` (partial class) and
`AI/Navigation/MPC/Editor/Types.Editor.cs` (partial struct + partial methods).

C# partials must compile in one assembly — the partial-ness *is* the private
-access mechanism. Conversion recipe per file:
1. Promote the private members the editor part touches to `internal`
   (`InternalsVisibleTo` already grants the editor assembly access).
2. Rewrite the partial as a plain class in the editor assembly (static helper
   or `[CustomEditor]`).
3. Instance callbacks Unity invokes (`OnDrawGizmos*`, `OnValidate`) either stay
   in the runtime class behind a minimal `#if UNITY_EDITOR` block, or convert
   to `[DrawGizmo]`-attributed statics in the editor class.

**C. Debug layer — entangled, goes last (PR-B tail):**
`AIDebugSettings` (ScriptableObject + `Settings/AI/AIDebugSettings.asset`),
`ArenaDebugOverlay` (MonoBehaviour, one live attachment on
`PlayerRig.prefab`), `UtilityLogger` (MonoBehaviour). No runtime code
references them, **but** `AICommander.Editor.cs` declares
`[SerializeField] AIDebugSettings debugSettings` inside the editor partial —
`UtilityPilot.prefab` and `PlayerRig.prefab` serialize that reference. These
types cannot move while Game.Core partials name them. Converting
`AICommander`'s partial must decide where that serialized field lives
(existing smell: fields declared under `#if UNITY_EDITOR` serialize in-editor
only, so editor and player disagree about the class layout).

**D. Inline `#if UNITY_EDITOR` blocks in runtime files** (`Navigator.cs`,
`Sector.cs`, `StateProfile.cs`, `UtilityChooser.cs`, `RingSpawner.cs`,
`SingleSpawner.cs`, `ChaseMetricsProbe.cs`, `LockLocalTransform.cs`): gizmos /
validation on the owning component. Normal Unity practice — out of scope
except where a PR-B conversion absorbs them for free.

## Sequencing

- **PR-A (this PR):** move the five §A files + this plan doc. Zero behavior
  change; compile + suite green is the proof.
- **PR-B (follow-on, one domain at a time):** AI commander/brain domain first
  (largest cluster, unlocks the §C debug layer), then MPC/nav, combat, ships,
  player, sectors, asteroids, camera. Each conversion runs the full suite; the
  in-Editor gizmo/inspector surface should be eyeballed for the touched
  domain.
- Alternative considered and rejected for now: `.asmref` files per nested
  `Editor/` folder (compiles the folder into `Game.Core.Editor` in place).
  Rejected because every such folder currently contains partials, which would
  break immediately; revisit per-folder once its partials are converted.
