# Gizmo Control Window — scene-global gizmo visibility, decoupled from selection

> STATUS: SHIPPED 2026-08-29. Arc #464 complete across three PRs: PR-A core (#470), PR-B sweep
> (#472), PR-C environment (this PR, #467) — Colliders toggle + facing-chevron ship marker. Living
> design record; kept, not deleted. Supersedes the selection-gated + per-instance-bool gizmo model
> for interactive editing. The capture lane (`GizmoCaptureProfiles`) is untouched by this arc; it
> only lends its category vocabulary as display grouping.

## Problem

Two axes control gizmo visibility today, and both are bound to per-object state:

- **Which objects draw** → editor **selection**. Every drawer gates on
  `[DrawGizmo(GizmoType.Selected, typeof(T))]`. Selection is overloaded (Inspector, transform
  handles) and fiddly — you can't hold a precise diagnostic set without it fighting every click.
- **Which subviews draw** → per-instance serialized bools (only 3 of 18 drawers even have them:
  Navigator, AsteroidField, PlayerCommander). No scene-global, category-level control exists.

Goal: a plain `EditorWindow` that controls gizmo visibility scene-wide, decoupling both axes from
selection.

## Ruling

A plain `EditorWindow` ("Gizmo View") holds a matrix of per-drawer toggles grouped under capture
categories, plus a scope dropdown. Drawers stop gating on `GizmoType.Selected` and instead draw
always, gated on a static flag registry + a scope predicate the window writes to EditorPrefs.

### Registry & flags

- **Key = `(component-type, subview)`.** Drawers self-register their subviews via a static call
  in an `[InitializeOnLoad]`/static-ctor hook — no central manifest that drifts from the drawers.
- Drawers read state through a static guard, e.g. `GizmoView.IsOn(type, subview)`.
- **Granularity: one toggle per *drawer* is the default row unit.** Navigator keeps its 4 existing
  bools as indented sub-rows under its drawer row. The other 17 drawers get **one** minted subview
  identity each. Finer splits are additive, done later only when a real need appears.
- **Existing per-instance bools are replaced**, not layered. Object-level scoping comes from the
  scope predicate, not from per-instance draw flags. (Fields removed:
  `Navigator.show*` — becomes the 4 sub-rows; `AsteroidField.drawNoiseHeatmap`/`drawChunkGizmos`;
  `PlayerCommander.showMouseGizmos`.)

### Scope predicate

- Single **global scope dropdown** for the window (not per-row): **All / Selected / Team N**.
- Resolution path (the one every drawer already uses to find its ship):
  `component.GetComponentInParent<Ship>()?.teamNumber`. Team is a plain `int` on `Ship`, mirrored
  in `ShipRegistry`. Caveat: the human player ship always reads team 0 regardless of the `team`
  arg threaded through `BuildAndWirePlayer`, so "Team 0" in a manual playtest scopes player +
  team-0 AI together.
- Drawers move from `GizmoType.Selected` to draw-always, filtered by a shared scope helper.
  No draw-throttling in v1: scope + default-off is the governor. All-everything tanking the scene
  view is a real observed finding for a later fix, not something to pre-engineer against.

### Category grouping

Window rows are grouped under the **capture taxonomy** (Steering / Combat / …) as **display
grouping only** — borrow the vocabulary from `GizmoCaptureProfiles`, do **not** reharness the
capture lane to consume the subview registry in this arc. (That deeper unification is a carded
follow-up.) Per-category master toggles (all-on/all-off) + a global all-off.

### Environment visibility (the user's two additions)

- **Colliders row** → drives Unity's **native** collider gizmo display via the annotation API (the
  machinery the native-gizmo arc already wrangles). This one row is the **documented exception to
  scope**: it is always **global** (Unity's collider gizmos can't be team/selection-scoped) and
  per-collider-type. Matches how the user uses it today — turn it on to see the whole environment.
  - **Open risk:** the native-gizmo arc has hit AnnotationManager warmth issues; verify the toggle
    API actually sticks before committing. Fallback if it proves flaky: a custom silhouette drawer
    (carded, not v1).
- **Ship marker** → a `Ship`-drawer subview, its own toggle, **default on**, scoped like other ship
  subviews. Representation: a **solid filled chevron/triangle** oriented to ship forward with a
  clear facing direction — **no thin lines, no circle/cross**. Render via `Handles.DrawAAConvexPolygon`,
  sized to stay readable at zoom (`HandleUtility.GetHandleSize`). Independent of the Colliders row
  (no wired dependency); in practice the user leaves the marker on and toggles Colliders as needed.

### Window UX

- Matrix (subview rows under collapsible category headers) + scope dropdown + per-category masters
  + global all-off + a **"N objects in scope" readout** (confirms the Team/Selected predicate
  caught what was meant). Nothing else in v1 (search box deferred).
- **Per-row appearance descriptor**: each drawer registers a one-line appearance string
  ("cyan predicted path w/ yaw ticks", "red facing chevron"), surfaced as a **hover tooltip**.
  Rides free on the registration signature; only cost is authoring ~20 short phrases. Color swatch
  deferred (would force touching every drawer to surface private color fields).

### Persistence & defaults

- **EditorPrefs**, keys namespaced `GizmoView.<ComponentType>.<subview>` (greppable, resettable
  wholesale). Not committed — one dev's debug toggles don't land in git.
- **Defaults: every subview off, except the ship marker on; default scope = All.** Fresh open then
  shows exactly one thing — clean facing chevrons on every ship, everything else quiet.

## PR shape

Split:

- **PR-A — core.** Registry + window + scope helper + convert the 3 bool-having drawers (Navigator,
  AsteroidField, PlayerCommander) as the proof-of-pattern. Native-collider API de-risk can ride
  here or in PR-C. This is where the design risk lives.
- **PR-B — sweep.** Convert the remaining 15 drawers off `GizmoType.Selected`, mint one subview id +
  appearance string each. Mechanical, parallelizable across subagents.
- **PR-C — environment.** Colliders toggle + chevron ship marker.

## Deferrals (card on merge of PR-A)

- Per-row / per-category scope override.
- Finer subview splitting beyond one-per-drawer.
- Custom-silhouette collider drawer (fallback if native annotation toggle is flaky).
- Color-swatch per row (fold into PR-B's per-drawer sweep if pulled forward).
- Deeper capture-lane unification: capture profiles consuming the same subview registry.
- Dedicated `DiagnosticsTarget` marker component as a scope predicate (if Team proves too blunt).
