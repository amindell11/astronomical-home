# Ship-paint evidence (#1014, PR #1015)

Evidence only. Per-ship assignments, settings, masks and studies here are not production conversions.
Round 2 began at tool commit `5a149876b`; round 2b regenerates Nightshade at `8d48f4dbc`. The owner retained brush contrast 3.2 and shadow depth 0.39.

## Round 1

- `crimson/`: original benchmark comparison against main's atlas, painted views, masks, settings and run report.
- `nightshade/`: original pale-blue palette study, painted views, masks, settings and report.
- `vanguard/`: **withdrawn: deferred to Vanguard's conversion (#1014 comment, 2026-10-15)**. Retained only as historical evidence; its face-majority atlas migration lost sub-face details and excluded the native cores.
- `lineup/`: historical game-light capture and original launcher/renderer; saved prefab rotations were not corrected, and Crimson/Vanguard used composite substitution.
- `lineup-neutral/`: historical camera-side key-light capture; superseded for pose and material binding by round 2.
- `fixture/`: fixture composite and smoke log (`SHIP_SMOKE_PASS`).
- `crimson_regions.py`, `vanguard_source.py`, `preview_material.py`, `tolerance.py`: original evidence helpers; `vanguard_source.py` is withdrawn with the Vanguard study.

## Round 2

Top-down, noses up. The final lineup contains Crimson, Nightshade and unchanged Valis only;
current prefabs above actual region-material candidates. Vanguard is deferred entirely.
The current Crimson masks contain no owner proof stroke; that separate demonstration is in `owner-stroke.png`.
Nightshade's base-coat study remains undecided; the decision belongs to its conversion PR.
The Nightshade lineup row is depth-study variant (c): production material numbers, plate/sweep on Violet, Plum and Edge; coverage 0.33, depth 0.39, side-wall depth 1.0.
Per-region painted-light colour is deferred to the Nightshade conversion PR; no parser change.

Neutral captures use a white directional key, intensity 1, Euler (15,-20,0), and flat ambient
(0.45,0.45,0.45). The game-light variant uses `Assets/Scenes/Lighting.prefab`, shadow maps off,
and flat ambient (0.42,0.44,0.48), matching round 1's lighting setup. Both retain identical poses.
Removing Vanguard removes one column: spacing stays 4.5 world units, height stays 9 world units,
and pixels per world unit stay unchanged (3000x2000 large; 480x320 game-scale).

Each file below is relative to `paint/round2/`:

- `fleet-final.png`: neutral Unity lineup, current prefabs above tool paint; Valis unchanged in both rows.
- `fleet-game-scale.png`: the same neutral lineup at native game-scale resolution.
- `fleet-game-light.png`: the same lineup under the game's Lighting prefab.
- `crimson-compare-top.png`: main atlas, current tool composite and any-channel difference; 1024-pixel Workbench views.
- `centreline.png`: old/new cap-boundary close-ups under neutral Unity light, with identical region binding.
- `canopies.png`: Crimson and Nightshade before/after the palette-only cockpit ruling.
- `owner-stroke.png`: an owner Light-mask stroke survives a re-run and appears in the composed preview and Unity.
- `brightness.png`: Crimson base, sweep, side-wall ramp, brush and full recipe under identical neutral Unity light.
- `brightness-grid.png`: brush contrasts 3.2/1.2/0.15 by shadow depths 0.39/0.18/0.02, plus today's atlas benchmark.
- `brightness-grid-game-scale.png`: all nine brightness choices at native thumbnail resolution; current is top-left.
- `nightshade-diagnosis.png`: controlled old, slots-only, slots-plus-caps and cockpit-excluded previews; dark strips return with slot identity.
- `nightshade-study.png`: evidence-only PaperColor proposal under neutral light; no direction approved.
- `nightshade-depth.png`, `nightshade-depth-game-scale.png`, `nightshade-depth-property-audit.json`: today and variants (a)-(e), native game-scale comparison, and accepted/rejected production material properties.
- `crimson/paint.json`: strict current recipe; coverage 0.33, depth 0.39, side-wall depth 1.0, brush contrast 3.2.
- `crimson/palettes.json`: Crimson's region palette in linear sRGB.
- `crimson/Shadow.png`: generated Crimson Shadow mask, without owner edits.
- `crimson/Light.png`: generated Crimson Light mask, without the proof stroke.
- `crimson/Ink.png`: generated Crimson Ink mask.
- `crimson/Emission.png`: generated Crimson indicator emission mask.
- `nightshade/paint.json`: strict current Nightshade recipe with explicit glass/frame exclusions.
- `nightshade/palettes.json`: current evidence palette; excludes the unapproved PaperColor proposal.
- `nightshade/Shadow.png`: generated Nightshade Shadow mask.
- `nightshade/Light.png`: generated Nightshade Light mask.
- `nightshade/Ink.png`: generated Nightshade Ink mask.
- `nightshade/Emission.png`: generated Nightshade louver emission mask.
- `PaintPreviewLineup.cs`: corrected three-column Unity renderer, explicit pose and import scale, both lighting setups.
- `paint_lineup.ps1`: batch launcher with explicit slot project, output, log and mask-root arguments.

### Crimson tolerance

`paint/tolerance.py` measures pixels whose maximum RGB delta exceeds 16/255 inside the union
of both ship silhouettes; mean delta is the mean maximum-channel difference in that silhouette.
Workbench textured compare, 1024 pixels per view; the round-2 main atlas blob is
`b0ad6ef9e01fb6a7b94a00d8451b811957a738aa`. The red comparison panel marks any channel change;
the numerical tolerance uses the 16/255 threshold. Owner cockpit reflections are intentionally
absent from generated paint, so their loss contributes to the difference from the atlas.

| View | Round | Silhouette px | Changed px | Fraction | Mean delta |
|---|---|---:|---:|---:|---:|
| top | 1 | 440194 | 83699 | 0.190 | 13.7/255 |
| top | 2 | 440211 | 92643 | 0.2105 | 16.08/255 |
| side | 1 | 108059 | 16523 | 0.153 | 13.2/255 |

Round 2 does not remeasure the side view. It fixes cap boundaries and material-slot assignment,
excludes glass and frames from generated treatments, composes previews from final masks, and
requires one home per control. No brightness tuning was accepted. These numbers record the
resulting drift; they do not claim a tolerance improvement.

### Reproduce the final lineup

Use the tool and sources in the PR's slot. Temporarily stage exported Crimson/Nightshade FBXs and
their generated palette material bundles at `Assets/Visuals/Ships/_PaintPreview/{Crimson,Nightshade}`,
and place `PaintPreviewLineup.cs` in the slot's `Assets/Editor/`. The published grayscale masks are
loaded from `-MaskRoot`, not imported over production textures. Invoke the published launcher via
`scripts/unity_access.ps1 -Action RunBatch`, with `-ProjectPath <slot>/src/Asteroids3D` and batch
arguments `-ProjectPath <slot>/src/Asteroids3D -OutDir <scratch> -LogPath <scratch>/unity.log
-MaskRoot <evidence-clone>/paint/round2`. Remove the temporary staged assets and release the lease.
The PNG sheets add labels outside ship silhouettes; no colour correction is applied to captures.

## 2026-10-18 addendum

`lineup/` and `lineup-neutral/` were re-captured by the prep session with every chassis root posed
by `GamePlane.Rotation` (the earlier files there showed the ships from behind). Round 2's
`round2/fleet-final.png` remains the lineup of record: it also carries the corrected pose and binds
the actual region materials, and it reflects the Vanguard deferral.
