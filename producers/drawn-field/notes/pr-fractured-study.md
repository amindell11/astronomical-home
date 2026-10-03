Exploration for #685 (map #684), now extending the accepted painted/outlined asteroid style across all ten shapes used by the game. Each keeps its source silhouette and gains fitted crease drawing, shallow impact bowls and a light-responsive scuff normal map. An editor-only study applies the converted surfaces to the real generated field and captures a full-field still, all-ten inspection views, and a 9.6-second flight by the real ship. Production asteroid assets, spawning/collision and ship art remain unchanged. This is a reviewable art study, not a production rollout.

## Why use mesh and paint?

The shell supplied outer contours but no internal definition. Automatic screen edges recovered overlaps yet made the rock scratchy and small ship details congested. After the user redirected work to the asteroid, a quiet mauve chipped-plane candidate proved too regular and low-contrast for their more specific reference. The new candidate follows that image's elongated shape, deeper clefts and cool/amber palette. Editable Blender sources remain in `art/asteroid-study/`; the active source is `AsteroidReliefStudy.blend`, containing the rock, separate surface-drawing mesh, and hidden sculpted relief source. The preceding drawing source is preserved.

The initial texture misread the reference shadows as painted black fractures. Those marks stayed fixed as lighting changed. The correction removes that darkness from albedo (fix ladder rung 1), disables texture-edge ink for this material, and derives the dark shapes from mesh recesses and real-time shadows. An opt-in cast-shadow-darkness property defaults to zero, preserving other materials, and the surface shader now includes installed URP soft-shadow quality variants. No new post effect.

The current medium-value albedo adds directional gouache brushwork and angular mineral patches. Exact ImageGen edit prompts and provenance accompany the packed Blender sources. An independently editable drawing mesh fits tapered ribbons to selected recess rims and plane changes. Six shallow impact bowls receive broken angular rim strokes, with a few paired chisel nicks. Selected ridge shoulders are flattened locally. Marks are explicitly placed, not randomly scattered. The drawing receives lighting but does not cast shadows, and is attached in both gameplay and inspection views.

The first linework draft became dotted at game scale; increased ribbon weight and surface offset made it continuous. The final finishing pass increases this asteroid contour to 5.5 pixels, modestly thickens surface strokes, and adds twelve selected crease continuations. The rock shape and brush paint remain unchanged in that pass; other treatments and ship contour widths remain unchanged. Intentional thin marks remain fixed on the material while the large shadow regions respond to lighting. Trial angular Boolean cuts produced flat-looking cavities and were removed in the earlier sculpt round.

Small surface cuts now add depth to the drawing: ten authored fans of beveled gouges and six shallow flake patches are sculpted into a hidden 176,802-vertex source, then baked to a 1024px tangent-space normal map. The first bake exposed the base triangulation; preserving the low mesh's smooth normal field outside the sculpted marks removed that global faceting. A normal map was chosen so the detail responds to light without adding fixed black paint or changing runtime topology. The existing runtime mesh, albedo, drawing and 5.5px contour remain unchanged. The material uses strength 0.85, and only this asteroid opts into the shader variant. Forward lighting and depth normals share the same mapped normal.

The archived single-rock comparison uses 72 native Unity frames of a stationary mesh with an orbiting light, plus a 72-frame fixed-light turntable. Its before/after pair changes only the relief. A separate still applies the committed scene-lighting prefab. That rig produces a darker, less graphic result, and the sculpt remains softer than the reference, with spherical UV compression near the poles. The normal map does not alter silhouette or cast-shadow geometry. High Fidelity is verified; the Performant pipeline disables main-light shadows. Collider/volume alignment and performance remain unverified for production use.

## How does the style extend to the whole field?

All ten source meshes are converted alongside the originals. Each Blender source retains its own low surface, fitted graphite ribbons, hidden scuff sculpt and packed normal/albedo images. Thirty-eight separated ridge sections guide the drawing on each shape; four individually placed shallow bowls receive broken impact rims. Paired nicks and grouped scuffs follow those ridge directions. The accepted brush albedo is shared. Field-specific near-black graphite uses 2.2 times wider fitted strokes, with a 7.5px contour and a stronger lit-side minimum width. Sixteen additional ridge paths per shape fill quieter areas without changing the base mesh or normal map. The material palette reduces the earlier yellow-orange cast while preserving cool shadows; the light rig is unchanged. Large shadows still come from real lighting.

The finishing drawing layer adds 14 separated patches of short hatch bundles, crossed scuffs and chisel ticks to each shape. Its finer line weight preserves the hierarchy of the main creases. Patches follow local ridge directions and skip crowded or sharply folding surface areas; each shape receives 32–63 strokes. The Fine crosshatching vertex group keeps the addition selectable in Blender, while the existing drawing export/material carries it at runtime. No further palette, lighting, base-mesh or normal-map changes accompany this pass. The preview compares directly against the preceding bold-contour pass so the added detail can be judged alone.

The initial FBX conversion flipped the depth axis. Correcting the Blender coordinate conversion preserved the game-space forms; the field regression now checks each converted surface against sampled original vertices. Fine ribbon sections initially broke up over rough mesh bends, so tighter surface sampling, a larger surface offset and increased stroke weight make the drawing continuous.

The study uses the existing UpdatingAsteroidField and pool, with a local BigFieldSettings clone bounded to load radius 65 and field radius 100. Its initial 74 asteroids include all ten shapes. The real Ship_1 moves about 31.9 units over 240 frames, ending at full health; new instances stream in during the flight. The camera uses canonical CaptureFraming and reads native Unity renders in the coordinated graphics batch lane. This avoids interfering with the user's live editor. The full-field still and clip use the accepted directional study light; a separate still uses the committed scene lights, and both use the game's Environment_2 background.

The quality review found that caching only MeshIndex skipped a pooled asteroid respawned with the same shape, because Initialize resets its surface. The cache now records SpawnEpoch (fix ladder rung 1: represent each spawn), with an explicit same-instance/same-shape reuse check asserting surface restoration and exactly one drawing/outline pair. Follow-up review found no remaining required changes.

This adds editable art assets and a temporary field presentation override. Production collision meshes, volumes and distant-detail/performance tuning remain unchanged and unverified for rollout. Frame rate describes video playback cadence, not measured rendering performance.

## Why did captures show a magenta asteroid?

The asteroid's material-less `LowLODMESH` child is a minimap proxy. The capture camera included Minimap layers that the world camera excludes, so the proxy rendered over the world asteroid. `GameViewEpisodeCapture.CreateRig` now excludes the three minimap layers (fix ladder rung 1: these proxies cannot render through this world-view camera).

The actual camera-construction regression failed with Minimap bit 2048 present, then passed after the correction. Matching gameplay frame 100 changed from 7,853 magenta pixels to zero. This explains #619. The exclusion also removes a ship minimap proxy; earlier gameplay ship images are superseded. The corrected footage exposes the real ship's darker appearance without changing its assets or materials.

## Which earlier corrections and experiments remain in this PR?

- Capture orientation uses the canonical game-plane viewing side; the original camera filmed the underside and mirrored horizontal directions. Its regression demonstrated red/green.
- Surface stencil exclusion prevents inverted contour shells painting fragmented asteroid interiors; three real-mesh graphics poses require a visible outer outline and clear interiors.
- Capture preparation honors the no-gizmo profile, backed by a red/green graphics regression.
- Editor-only outer-contour and screen-ink comparison scenarios remain available. Temporary renderer features restore in `finally`; no renderer asset receives a persistent feature.

The rejected 282-triangle flat-normal asteroid and generated crease-mask approach were removed. Texture suppression alone kept jagged lighting islands; shared-position fighter normals reduce those while rounding some panel edges. Ship work is paused for asteroid review.

## Validation

- Current full-field run `20260926-190932-summary.json`: 1 passed, 0 failed, including source-coordinate proximity for all ten shapes, all-ten field membership, visible rendered detail, ship displacement and same-shape pool reuse. The preceding combined run `20260926-015843-summary.json` passed 4/4, including the existing light-response and ink tests.
- Current field artifacts: `results/asteroid-field-study/20260927-021051`. Native 1600�900 stills and 240-frame flight; encoded 25fps MP4 decoded successfully. Preview and evidence copied outside the recyclable worktree. The current preview includes side-by-side previous/current detail, full-field and all-ten views.
- Final field changed-line ReSharper ratchet: 0 blockers, 67 report-only/touched-file findings. Combined quality review's pooling correction is included; subsequent bold-line, palette, line-density and fine-hatching reviews found no required changes.

- Earlier asteroid gameplay scenario passed (before the clean-albedo correction): `20260924-232810-summary.json`; capture `20260924-232830-DrawnAsteroidPaintScenario`. Real rotation, left/right banking and damage assertions; 900 motion samples and 899 captured frames at 50 Hz.
- Single-rock lighting regression: red with 4,066 fully lit black pixels; green with zero. The final sculpt changes 15,671 of 29,626 surface pixels between dark and lit under opposing lights. The drawing affects 1,280 pixels, about 4.3% of the visible stone; the regression bounds it to sparse linework.
- Earlier single-rock relief native graphics run `20260926-005310-summary.json`: 3 passed, 0 failed, including both ink tests. Relief changes 876 visible pixels under opposing lights; 167 reverse their bright/dark response. The depth-normal pass changes 577 pixels, verifying that rendering effects receive the relief too.
- Earlier single-rock 72-frame light sweeps in `results/asteroid-light-study/20260926-075339` (light sweep and turntable); encoded clips decoded to verify frame count. Preview and evidence copied outside the recyclable worktree. The current preview includes side-by-side previous/current detail, full-field and all-ten views. Earlier 144-pose mesh turntables and gameplay footage are archived, not current visual evidence.
- Minimap capture regression: red before, green after.
- Earlier unchanged coverage: two internal-overlap graphics tests, three asteroid contour graphics tests, fourteen capture EditMode tests and four native gizmo recovery tests.
- Combined quality review: no required findings for the relief pass; tangent handedness, shared forward/depth-normal mapping, opt-in material behavior and regression coverage checked. No review edits required. That earlier tree passed the changed-line ReSharper ratchet with zero blockers and 63 report-only findings.
- Scoped results are not full-suite merge proof. No 1080p/60 fps or finished art-match claim.

Reproduce the light-response check: run `unity_test_agent.ps1 -Mode PlayMode -WithGraphics -TestFilter AsteroidLightingPlayModeTests` against the leased project. The test emits `ASTEROID_LIGHT_STUDY` with its frame directory.

Reproduce the scene scenario: queue `DrawnAsteroidPaintScenario`, then route `CaptureScenarioPlayModeTests` through the warm capture lane. Assemble the producer-emitted frame directory with `scripts/capture/assemble.py --keep-frames`.

Scope conservation: comparison shaders/scenarios, editable asteroid art studies, observed capture/hangar corrections and their regressions. No production rollout, new dependency or saved renderer-feature installation. Generated editor settings excluded. This PR does not close #685.

Vocab: none




