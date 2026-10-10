# Nightshade breakup — large sections

Actual Unity Game View recording of Nightshade dying through its normal damage event.
Owner approved starting breakup and chose large sections. Final motion review remains pending.

![Breakup](Nightshade-breakup.gif)

[MP4](Nightshade-breakup.mp4)

The 13 source groups keep wings, fins, tails, cockpit, engine and spine recognizable.
The central hull disappears in the flash; smaller fins fade before the large wings and engine.
The ship retains its current blue palette and the owner's saved Unity lighting/emission tweaks.

## Validation

- Geometry lock verified; adding source collections changed no geometry, UV or material fingerprints.
- Blender smoke: intact and debris exports contain the same painted triangle multiset, including UVs and normals.
- Seven Python contract tests passed.
- 48/48 ship EditMode checks passed, including the imported paint maps and High Fidelity hit flash.
- Existing Crimson/Vanguard/Valis breakup tests passed; Nightshade's new test verifies 13 pieces,
  every material/position/UV corner exactly once, one explosion, inherited velocity, fade, cleanup and reset.
- Native capture scenario passed. Mid-burst frames were viewed before encoding. GIF is 25 fps;
  MP4 is 50 fps. Both encodes were decoded to verify length. This close-up hides the ship HUD.

## Rebuild

Run the repo's `ship_lock --mode verify`, then `ship_export --debris` against Nightshade's ship.json.
The emitted FBX and sidecar are in `export/`. The source collections own grouping; paired `.L/.R`
collections partition evaluated triangles without cutting or applying source modifiers.
Import the FBX into `Assets/Visuals/Ships/Nightshade/Breakup/NightshadeBreakup.fbx`.
Run `BuildNightshadeBreakup.cs` through Unity CLI `run_script` after updating its scratch path.
It reads the committed `art/ships/nightshade/breakup.json` numeric settings and builds the legacy
Animation clip and prefab. Unity reorders FBX submeshes: material bindings use imported material
identities, not the sidecar's slot ordinal. The red/green continuity test caught that difference.

The script is evidence-only per the ship-art workflow; the generic prefab/motion builder remains
part of issue #868. All runtime behavior uses the existing ShipBreakupVisual/ShipBreakupDebris.

![Intact](intact.png)
![Burst](burst.png)
![Drift](drift.png)
