# Asteroid Mesh Re-Pivot + Mean-Radius Bake

**Status:** Item 1 (re-pivot) DONE — PR #87 merged `ee684ad7` (import-time
`AssetPostprocessor`, volume centroid; verified ~0% offset, suite 322/0/3).
Item 2 (`cachedMeanRadius` bake) folded into the multi-sphere PR-2 `OnValidate`
work. Item 3 (verify) done for re-pivot.
**Date:** 2026-07-08
**Related:** Chase_Nav_Track_B (mean-vertex radius, clip-over-berth), Multi_Circle_Asteroid_Obstacles

## Problem

The cheap asteroid circle (SphereCollider + AI-facing `Radius`) is centered on
the mesh **pivot** — `cheapCollider.center` is never set, and `DetectedObstacle`
is built from `transform.position`. For meshes whose vertex mass is offset from
the pivot, the circle is systematically lopsided, and the rigidbody's far-field
rotation center (COM from the sphere collider = pivot) is not the visual
center, so the rock appears to gyrate around an invisible point. There is also
a plausible dynamics hitch at the 75-unit collider-LOD boundary where the
auto-computed COM jumps between pivot (sphere only) and the convex mesh
centroid (verify empirically — see Risks).

### Measured offsets (2026-07-08, `Tools/Asteroids/Log Centroid Offsets`)

Vertex-centroid distance from pivot, as % of mean-vertex radius
(`SpawnSettings.asset` meshes, local/unscaled units):

| Mesh | offset/meanR | offset | meanR origin → centroid |
|---|---|---|---|
| Asteroid3_LOD0 | **20.4%** | 0.386 | 1.890 → 1.864 |
| Asteroid2_LOD0 | **13.5%** | 0.218 | 1.617 → 1.604 |
| Asteroid4_LOD0 | **11.7%** | 0.340 | 2.893 → 2.878 |
| Asteroid5_LOD1 | 4.7% | 0.140 | 2.991 → 2.988 |
| Asteroid1_LOD0 | 4.1% | 0.050 | 1.224 → 1.223 |
| Asteroid8_LOD1 | 2.6% | 0.028 | 1.054 → 1.053 |
| Asteroid6_LOD0 | 1.5% | 0.044 | 2.868 → 2.868 |
| Asteroid7_LOD0 | 0.8% | 0.017 | 2.032 → 2.032 |
| Asteroid9_LOD0 | 0.6% | 0.011 | 1.791 → 1.791 |
| Asteroid10_LOD0 | 0.7% | 0.015 | 2.307 → 2.307 |

Takeaways: only Asteroid2/3/4 matter; re-centering barely shrinks the radius
(≤1.4%) — the win is **placement** and rotation realism, not a tighter circle.
The mean-from-origin radius does NOT absorb the offset (padding ~0.026 vs
0.386 sweep for Asteroid3), so "the current circle covers the wobble on
average" is false.

## Decision (REVISED 2026-07-08 — premise corrected)

**PREMISE CORRECTION:** an earlier draft claimed the meshes are embedded
sub-assets inside `SpawnSettings.asset` with "no FBX import path". That is
**wrong for the visual meshes** — `MeshInfo.mesh` is an FBX *reference*
(`{fileID: 4300000, guid: <Asteroid3.fbx>, type: 3}`); the source FBXs
(`Assets/Visuals/Environment/Asteroids/HD_Asteroids/Models/Asteroid1..10.fbx`)
exist. Only some pre-cooked `_collider` meshes are embedded — and for the only
off-pivot rocks (Asteroid2/3/4) even the collider is an FBX ref (same guid).

**Re-pivot the meshes at FBX import time via an `AssetPostprocessor`**
(`OnPostprocessModel`), not a destructive `.asset` vertex rewrite and not
Blender. The postprocessor, gated to the `HD_Asteroids/Models/` path, computes
the signed-tetrahedron **volume centroid** of the visual (LOD0) mesh and
translates **all meshes in that model by the same vector** (`-centroid`),
recomputing bounds. This fixes, with zero runtime cost, in one committed editor
script: far-field gyration, the collider-LOD COM jump, the lopsided AI circle,
and the cheap-sphere fit — because the imported geometry itself is recentered,
so MeshFilter, rigidbody auto-COM, and the AI `Radius`/lobes all see a
centroid-pivoted mesh. `AsteroidController.Initialize → MeanVertexRadius`
(measured from local origin) then yields the right radius AND center unchanged.

Why this mechanism:
- **Reversible / non-destructive** — the source FBX is untouched; recentering
  is applied to the *imported* copy and regenerated from source every import.
  Delete the postprocessor → reimport → original pivots return. No git-backup
  ceremony, no one-way rewrite.
- **In-repo & deterministic** — committed editor script; every teammate's
  import matches. Idempotent: a re-centered import recomputes a ~0 centroid.
- **Visual + collider stay registered** — same FBX, same translation vector.

Rejected alternatives: (a) destructive `.asset` vertex rewrite — not even the
right op, since visual meshes are FBX refs; (b) Blender source edit — external,
manual, unnecessary; (c) `cheapCollider.center` + world center into
`DetectedObstacle` — more plumbing, and an off-pivot center *orbits* the
rotation center as the rock tumbles, reintroducing the snapshot-decay problem.

## Scope

1. **Re-pivot via `AssetPostprocessor` (editor, import-time).**
   `AsteroidPivotPostprocessor : AssetPostprocessor.OnPostprocessModel`, gated to
   `Assets/Visuals/Environment/Asteroids/HD_Asteroids/Models/`. Compute the
   **volume centroid** (signed-tetrahedron; NOT vertex mean) of the visual
   (LOD0 / highest-vertex) mesh, translate ALL meshes in the model by that same
   `-centroid` vector (visual + collider stay registered), recalc bounds.
   Runs on import from the untouched source FBX → reversible, deterministic,
   idempotent. (NOT a destructive `.asset` rewrite — visual meshes are FBX refs.)
2. **Bake `cachedMeanRadius`.** Add the field to
   `AsteroidSpawnSettings.MeshInfo` next to `cachedVolume`; populate via
   `OnValidate` recompute (editor-only) so it can never go stale on a mesh
   swap — and cover `cachedVolume` with the same recompute (fixes its latent
   staleness too). `AsteroidController.Initialize` reads the field;
   `MeanVertexRadius` remains as the editor-side computation; delete the
   static `MeanRadiusCache`. Rider benefit: runtime no longer needs
   CPU-readable meshes (read/write could later be disabled).
3. **Verify.** Re-run the offset report (expect ~0% everywhere); full test
   suite for field-layout fallout (spacing/overlap effectively shifts by up to
   ~0.4 local units × scale for Asteroid2/3/4); keep the radius EditMode tests
   (`AsteroidRadiusEditModeTests`) pointed at the editor-side computation.

## Out of scope

- `DetectedObstacle`/center plumbing (moot once pivots are correct).
- The collider-LOD swap distance (`detailedColliderEnableDistance`).
- Multi-circle / polygon obstacle representations (separate plan).

## Risks / open checks

- Verify Unity actually recomputes rigidbody COM on collider enable/disable
  (the LOD-boundary hitch hypothesis). The fix is right regardless.
- Deterministic-field golden data / hand-placed asteroids in test scenes get a
  sanity pass — pivot positions are unchanged but geometry shifts relative to
  them.
- ~~Destructive asset edit~~ — MOOT: the postprocessor is non-destructive and
  reversible (source FBX untouched; delete the script + reimport to revert).
- The import-time recenter shifts the *visual* geometry of the display prefabs
  under `HD_Asteroids/Prefabs/` (they reference the same FBX). Sanity-check those
  aren't relied on for a fixed world placement; they appear to be showcase.

## Tooling

Diagnostic script: `Assets/Scripts/Editor/AsteroidCentroidOffsetReport.cs`
(menu `Tools/Asteroids/Log Centroid Offsets`; also runs headless via
`-batchmode -executeMethod AsteroidTools.AsteroidCentroidOffsetReport.Run`).
Keep it — it's the verification step for the re-pivot.
