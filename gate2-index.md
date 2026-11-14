# Outpost kit, gate 2: blockouts, 2026-11-14

The three shapes the owner took forward from gate 1, rebuilt as editable files: the repair station's gimbal dock, the mod station's kite and the armory's magazine drum.

| File | What it is |
|---|---|
| `gate2-gimbal.blend`, `gate2-kite.blend`, `gate2-drum.blend` | One blockout per station |
| `gate2-gimbal-blockout-sheet.png`, `gate2-kite-blockout-sheet.png`, `gate2-drum-blockout-sheet.png` | Top, front and side views with sizes and the trigger circle, four more orientations, and a game-size row |
| `gate2-stations.py`, `gate2-sheet.py` | The build and sheet scripts |
| `gate2-inspect_blend.py` | Opens a blockout, prints its part tree, and checks that moving a part carries its meshes |

## How a blockout file is laid out

- One root empty named for the station. Rotate it to tumble the whole thing.
- Under it, an empty per part, drawn as three arrows at the part's pivot: `Inner ring`, `Sail 1`, `Magazine 3`, `Crew pod` and so on. Moving, turning or scaling the empty carries every piece of that part. Some parts sit under others: the repair arms under `Inner ring`, each sail's panels under its sail.
- Under each part, one mesh per colour, named `<part> / <colour>`.
- The pivots are placed for the obvious edits. Turning `Inner ring` swings it through the outer ring. Turning `Sail 1 panels` about its local X changes the sail's pitch. Scaling a `Magazine` along its local Z changes its length.
- The collection `Ink preview (render only)` holds the outline shells. It is hidden in the viewport and cannot be selected. Each shell shares its part's mesh, so an edit to the part shows in the outline at render.
- The collection `Reference` holds the 16 u trigger sphere and a mark the size of the player ship.
- No object is scaled. One Blender unit is one game unit.

## How to regenerate

```
blender -b --factory-startup --python gate2-stations.py -- <dir> blockout gimbal kite drum
python gate2-sheet.py <dir> <sheets dir> blockout gimbal kite drum
```

Regenerating overwrites the `.blend` files from the script. After the owner's first hand edit the `.blend` is the source of record and the script must not be run over it (`doc/agents/art-pipeline.md`).

## What was checked

- Each station is sized so its longest side is 29 u, the size of the repair station in the game today. That scaled the gimbal by 1.02, the drum by 1.06 and the kite by 1.22 from the gate 1 sheets.
- The trigger is a sphere of radius 16 u. Parts of each station reach past it: 18.1 u on the gimbal (the crew pod), 17.6 u on the kite (a sail corner), 17.0 u on the drum (a magazine end). The player does not need to touch those parts.
- Open space at the centre: 4.6 u to the nearest hull on the gimbal and 6.1 u on the drum. The kite's centre holds the prize, with hull 2.4 u out, so the ship sits in a bay between two sails.
- Triangles: gimbal 4,324, kite 4,744, drum 5,368.
- These are Blender previews, as in gate 1. Nothing has been in Unity.
