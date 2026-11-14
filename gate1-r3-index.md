# Outpost kit, gate 1 round 3: Blender massing, 2026-11-14

Three station shapes built as real meshes, for #1384. The image-model sheets from rounds 1 and 2 are not the target any more (#1381, 2026-11-14).

| File | What it is |
|---|---|
| `gate1-r3-family-sheet.png` | The three stations at one scale, large and at game size, lit and dark |
| `gate1-r3-gimbal-sheet.png` | Repair station: eight orientations and a game-size row |
| `gate1-r3-lantern-sheet.png` | Mod station: the same |
| `gate1-r3-drum-sheet.png` | Armory: the same |
| `gate1-r3-gimbal.blend`, `gate1-r3-lantern.blend`, `gate1-r3-drum.blend` | The models, one object per part and material |
| `gate1-r3-stations.py` | Builds and renders all three in headless Blender 5.1 |
| `gate1-r3-sheet.py` | Lays the renders out as the sheets (Pillow, NumPy, OpenCV) |

## How to regenerate

```
blender -b --factory-startup --python gate1-r3-stations.py -- <renders dir>
python gate1-r3-sheet.py <renders dir> <sheets dir>
```

## What the pictures are and are not

- Every station is shown at the same eight orientations: face, reverse, edge-on, and five tumbles drawn from one fixed seed. None was picked for looks.
- The game-size row is a downscale of those renders. 29 u spans about 260 px of a 1280x720 frame in the repair station capture from PR #1344, and the row uses that ratio.
- These are Blender EEVEE renders. The three-tone shading is a preview material, the outline is an inverted shell on each part, and `gate1-r3-sheet.py` adds the bloom afterwards. None of it is the game's `DrawnSurface` or `DrawnContour` shader, and nothing here has been in Unity.
- Triangle counts: gimbal 4,324, lantern 6,704, drum 5,368. Today's repair station is about 177,000.
- Light colours follow the minimap find colours from PR #1508 (green, gold, red), brightened for lamps. The owner has not ruled on world light colours.
