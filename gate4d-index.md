# Outpost kit, gate 4: sunk lamps and baked hash marks, 2026-11-15

The owner ruled on 2026-11-15: "Bake hashmarks, fix light and rebake, route b approve" (#1384, issuecomment-6908271521). They picked the fix for all five lamp types and a Unity boot once the rebake was ready.

The Unity side and the tools are on `task/outpost-kit-engine` at `71f42ae90c0449b01915392dc764bab9c6cb6273`, with no PR. Nothing in the game references the stations yet.

| File | What it is |
|---|---|
| `gate4d-lamps.png` | Each lamp type in the game at size 2.5 and size 6: the repair dock's berth light, inner-ring end lamp and repair-arm lamp, the armory's magazine-mouth lamp, the mod station's sail-edge lamp |
| `gate4d-compare-repair-z15.png` | The repair dock at size 15 before and after, with 2x crops of the berth arm and the ochre plate |
| `gate4d-sheet-copper-z15.png`, `gate4d-sheet-copper-z25.png` | Copper Turquoise: every station, open, in use and dark, with the game's 4x MSAA |
| `gate4d-marks-in-ink.png` | Two spots per station from the new `Ink` masks at 4x, inverted: the baked marks among the hatching |
| `gate4d-bake-preview-<station>.png` | Blender's review render of each bake, top and tilted |
| `gate4d-detail-stations.py` | The station builder with the lamp changes. `oldlamps` builds the accepted lamps, `norender` skips the preview renders |
| `gate4d-export_station.py` | Blender export, now dropping the mark meshes |
| `gate4d-BuildOutpostStations.cs` | Editor script: ink strength 1, no mark material, retires `Outpost Ink Mark.mat` |
| `gate4d-focus_lamp.cs` | Capture step: centres the camera on a lamp |

## Lamps

- Each lamp that butted against its host is sunk about half its depth and framed by a thin slate lip, larger than the lamp and smaller than the host's face. The berth light uses the art session's numbers. The other four follow the same rule.
- The builder reproduces the accepted models exactly with the old lamps: every mesh matches the `.blend` on the branch in vertex count, face count and world position. With the new lamps, only the lamp meshes and the slate meshes of the parts that gained a lip change.
- Each station is sized with the old lamps, so moving a lamp cannot rescale it. The mod station's old sail-edge lamps were its outermost points.

## Hash marks

- The mark meshes are baked onto each station's `PaintUV` (Cycles, selected to active, world space) at 4096 and filtered down to 2048, then dropped from the export.
- `Ink = max(hatch * (hatch r + hatch g), marks)`, with `hatch` 0.13 from `outpost-colour.json`. The hatching keeps its accepted strength and a mark reaches full ink. `_PaintInkStrength` on the hull materials is now 1, and changing the hatch strength now needs a rebake.
- Triangles, contours included: repair dock 20,024 to 17,364, mod station 19,086 to 17,434, armory 27,652 to 18,104.
