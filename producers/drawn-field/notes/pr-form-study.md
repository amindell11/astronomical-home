Exploration for #685 (map #684), now focused on the asteroid. The current candidate uses a new chipped-plane Blender mesh and a generated mauve/slate painting with sparse fissures. Texture marks stay attached during rotation; the existing shaders supply lighting and a thicker outer silhouette, with automatic internal screen ink disabled. Production asteroid configuration and ship assets remain unchanged. This is an art study, not a production rollout or accepted reference match.

## Why switch the asteroid to mesh and paint?

The outer shell could not produce internal lines. The screen-edge experiment recovered overlap boundaries but traced small rock details into scratchy marks. The user redirected the work to asteroid mesh/texture authoring. The new asset uses broad faces, beveled edges and two larger recesses instead of the rejected 282-triangle flat-normal substitution. The albedo was generated using the built-in ImageGen tool with the selected hangar image as style input; the full prompt and editable, texture-packed Blender source live in `art/asteroid-study/`.

Two raw Unity turntables compare 144 matching orientations under the same camera/light at 24 fps. The new `DrawnAsteroidPaintScenario` also exercises the actual gameplay/hangar scene. Collision and volume intentionally retain the original configuration: the visual prototype is not ready for gameplay promotion. The remaining art gaps are a somewhat regular silhouette, spherical-UV stretching near the poles, and crack placement that does not yet follow each modeled fracture.

## What caused the magenta overlay?

The asteroid's `LowLODMESH` child is a minimap proxy with no regular material. The capture camera included Minimap layers that the world camera excludes, so the proxy rendered over the visible asteroid. `GameViewEpisodeCapture.CreateRig` now excludes the three minimap layers (fix ladder rung 1: make those objects unrenderable by this world-view camera). A regression exercises that actual camera construction, requires the minimap layers to be absent and the Asteroid layer to remain visible. This explains #619; it was not a texture or MSAA defect.

The regression failed with `Minimap Expected: 0 But was: 2048` before the correction and passes afterward. Matching gameplay frame 100 dropped from 7,853 magenta pixels to zero. The exclusion also removes a ship minimap proxy, so older gameplay ship captures are superseded; the corrected footage exposes its darker actual appearance, with ship assets/materials unchanged.

## What did the preceding contour experiment establish?

An isolated 768×768 ship render showed 5,390 changed pixels from the outer shell and zero added interior ink. Increasing shell width cannot define canopy or overlapping hull sections. The new temporary full-screen pass recovers those boundaries in close-ups. Both comparison sides use the same meshes, cream/orange/violet palette, lighting and renderer feature; only ink strength changes. Fighter positions/UVs are unchanged, with shared-position visual normals retained from the previous round. The thicker 4.5 px outer contour remains on both sides.

That result was incomplete: the rock's fine creases became scratchy marks, and narrow ship details filled with black or fragmented at gameplay scale. Turning gameplay MSAA off reduced some fringes without resolving the main problem. No evidence supported a UV flip or a normal-renormalization correction. These earlier scenarios remain as comparison evidence.

## Which capture corrections remain necessary?

- `CaptureFraming` uses the canonical game-plane basis on the normal game's viewing side. The original camera filmed the underside and mirrored horizontal directions; its orientation regression was red before the change and green afterward (fix ladder rung 1).
- Drawn surfaces write URP user stencil bit 0; the outer contour excludes those pixels. This prevents inverted shells painting fragmented asteroid interiors. Native hangar textures use 24-bit depth/stencil. Three real-mesh poses demonstrate red/green and require a visible outer contour (rung 1).
- Capture preparation honors the no-gizmo profile rather than enabling GameView/collider guides (rung 1; initial graphics regression red/green).

All current evidence uses the corrected camera. The screen-ink feature is installed only by editor-only comparison/test code and removed in `finally`; it does not persist renderer-asset changes.

## What was verified?

- Current asteroid paint scenario passes bank/rotation/damage assertions; 144-pose raw Unity turntables exercise the new mesh and wrapped texture through a full turn. The actual-rig minimap regression demonstrates red then green.

- Two new graphics tests pass for internal overlap lines under orthographic and perspective cameras, while requiring broad surfaces to remain clear.
- Three asteroid outer-contour graphics regressions pass after the new surface depth/normal pass.
- Current outer-only and internal-ink captures pass real bank, rotation and damage assertions. Packaging checks all 900 motion rows for exact equality, including position, bank, asteroid quaternion and health.
- Both 1920×1080 clips decode to 899 captured frames at the existing 50 Hz cadence. Direct 768×768 Unity stills provide an on/off comparison without changes to mesh, color or light. The local review package is outside the recyclable worktree.
- Fourteen capture EditMode tests and four native gizmo recovery tests passed in earlier rounds covering the unchanged capture corrections.
- ReSharper changed-line ratchet passes with zero blocking findings and 51 report-only/touched-file findings. Combined quality review found no required changes, including temporary renderer-feature restoration; no review edits were made.
- These scoped results are not full-suite merge proof. Art acceptance and the 1080p/60 fps target remain unverified.

Reproduce current pair: queue `DrawnExplorationScenario` (outer-only) or `DrawnInkScenario` (internal lines), then route `CaptureScenarioPlayModeTests` through the warm capture lane. Assemble the producer-emitted frame directories with `scripts/capture/assemble.py --keep-frames`. `DrawnControlScenario`, `DrawnSurfaceScenario` and `DrawnContourScenario` remain available for earlier comparisons.

## Which alternatives were rejected, and what remains open?

The 282-triangle flat-normal asteroid study was rejected by the user and removed. Frontal shell attenuation and world-space extrusion did not fix interior fragments; surface stencil exclusion did. A generated UV crease mask produced small broken marks instead of deliberate boundaries and was removed. Texture suppression alone retained jagged lighting islands; shared-position fighter normals reduce those at the cost of rounding some panel edges. Read-only mesh data is required because the committed fighter is not CPU-readable in PlayMode.

The low-detail study hid the magenta minimap proxy rather than fixing it; the capture-layer correction above now addresses its cause. Earlier clips showing the overlay are superseded. The fighter's detailed, uneven geometry and indiscriminate edge detection remain obstacles to its reference match; ship work is paused while the user evaluates the asteroid direction. Additional-light/quality-tier coverage is unverified. Background/lighting work remains on arc #678; recheck against #681 when available.

Scope conservation: comparison shaders/scenarios, one editable asteroid mesh/texture/material candidate, graphics regressions, and shared capture/hangar corrections supported by observed failures. No production material rollout, saved renderer-feature installation or new dependency. Generated editor settings are excluded. This PR does not close #685.

Vocab: none
