# Gizmo → Painter Migration ([#294](https://github.com/amindell11/astronomical-home/issues/294))

Migrate the legacy editor-only gizmo files onto the painter / diagnostic-canvas
contract, and replace `AIDebugChannel` gating with painter-name atoms grouped
into observation environments. Design locked with the user 2026-08-06 (arc
decisions in memory `project_gizmo_painter_migration.md`); this doc adds the
slice sequencing and the frozen Slice-A decision brief.

**Goal.** Every runtime diagnostic drawn once, against `IDiagnosticCanvas`,
visible in both recorded clips and the live scene view; one selection grammar
(painter names + presets) driving both frontends; nothing drawn unless asked.

**Non-goals.** Authoring/edit-time gizmos stay Gizmos-native (field layout
preview, lobe bake, spawner markers). No new canvas primitives beyond `Rect`
and `Arc`. No EditorWindow. No probe-sourced painters (future extension point,
registry comment marks it).

## Slices

- **Slice-A `painter-spine`** — gating system + canvas primitives + proof
  migrations (Scout, LockOnSensor). Brief below. Everything after it is rote.
- **Slice-B `painter-ai-batch`** — NavigatorSteering (minus control-bar panel),
  Gunner, AICommander migrations.
- **Slice-C `painter-combat-ships-batch`** — Missile, Missiles, Lasers,
  MovementController, DamageController, ObserverCam; delete `SuperGizmos` and
  `AIDebugSettings`/`AIDebugContext` when the last consumer falls.
- Slice-B/C are mechanical once A lands and may fan out to parallel agents.
- Deferred, not sliced: AsteroidControllerGizmos (needs a PainterContext
  service extension — raise the seam question when a capture wants it);
  PlayerCommanderGizmos excluded (mouse-input debug, no mouse in captures).

## Slice-A decision brief (frozen 2026-08-06)

**Scope.** New gating spine end-to-end; `Rect` + `Arc` on both canvas
backends; migrate ScoutGizmos + LockOnSensorGizmos to painters; re-gate
PolicyGizmos; delete the policy fan; docs/tests updates in-diff.
**Non-goals.** No other file migrations; no SuperGizmos changes; no
AIDebugSettings deletion (survives for unmigrated files).

### Forks (with why)

1. **Slicing = spine-first.** Design risk concentrates in one reviewable PR;
   follow-ups become mechanical — the shape the harness-lane arc itself used
   (#231/#246). Proof pair Scout + LockOnSensor: between them they exercise
   `Rect`, `Arc`, the ray-fan cone, and gating adoption by a previously
   ungated file.
2. **Gating state = EditorPrefs-backed editor static `DiagnosticGate`**, not a
   ScriptableObject successor. A tracked settings asset + nothing-on-by-default
   means every toggle dirties the working tree — real noise in a multi-agent
   workflow where sessions read `git status`. Keys are **project-path-qualified**
   (pooled worktree paths are stable and reused; population ~8, no growth).
3. **User surface = checkable `Diagnostics/` menu only** (presets + atoms
   submenu + Draw unselected ships + Clear all). Agents drive the same items
   via `execute_menu_item`; an EditorWindow (repo's first) is deferred until
   the menu chafes — additive later, shared state makes it throwaway-free.
4. **Presets are code-defined** in the runtime registry so `SessionSpec`
   validates preset names in `RL_HARNESS_PAINTERS`. Preset click **replaces**
   the active set (a preset is a modal lens); atoms toggle individually on top.

### Assumptions (user-ratified)

- `Rect(Vector2 center, Vector2 size, Color)`;
  `Arc(Vector2 center, float radius, Vector2 fromDir, float sweepRad, Color)`.
  CaptureDraw renders arcs as segmented polylines like its rings.
- One flat namespace for atoms + presets; registry throws on duplicate
  registration. Env var accepts a mix, expands presets, dedupes.
- `ParsePainters(null)` → empty (**default flip** — was `ship-diagnostics`);
  flip the RLSessionSpecEditModeTests assertion, `training/rl/README.md`, and
  the game-capture skill line in the same PR.
- Coexistence: unmigrated files keep `AIDebugChannel`; each migration deletes
  the enum members it orphans. Slice-A kills `Scanning`, `Policy`, and `Info`
  (already dead). Explicit flag values → serialized asset undisturbed.
- Shims stay thin `[DrawGizmo]` per-subject hooks, gated by
  `DiagnosticGate.IsActive(name)`; menu toggle "Draw unselected ships"
  replaces `alwaysDrawGizmos`. Selection-scoping stays a shim nicety, not part
  of the gating contract.
- `ship-diagnostics` remains env-var-only (pair-shaped, no component hook).
- Painter construction per `PolicyPainter.Cache` precedent
  (`GetComponentInChildren` from context ships, null-tolerant).
- Names `scout-scan`, `lock-on`; kebab per `ship-diagnostics`.
- Tests: extend RLSessionSpecEditModeTests (preset expansion, default flip,
  duplicate-name throw); Rect/Arc ride existing capture PlayMode coverage;
  headless-safe, no RequiresGraphics additions.

### Blindsider resolutions

- **Prefs scope** → per-worktree keys (see fork 2).
- **Preset click** → replace (see fork 4).
- **Policy fan** → user ruled: delete the fan visualization entirely
  (`DrawFan`, `CaptureFanDepth`, the `fanDepth` param, and
  `AIDebugSettings.policyFanDepth`). Painter keeps velocity, commanded facing,
  nose, label.

### Vocabulary

**observation environment** (coined here, in glossary): a named, code-defined
preset of painters selected as a unit — the lens you view a run through.
2D-native authoring rule for migrations: re-express each diagnostic in
plane-space semantics (cone → ray fan/triangle, sphere → ring, box → rect);
never project 3D shapes at draw time. Camera-facing billboard UI does not map
onto the diagnostic canvas and is dropped or left editor-only.
