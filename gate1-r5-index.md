# Outpost kit, gate 1 round 5: the kite reworked and faction dress, 2026-11-14

Follows the owner's answer to round 4: "I prefer kite I think, but I agree about dress".

| File | What it is |
|---|---|
| `gate1-r5-family-kite-sheet.png` | The trio as it stands: gimbal dock, kite, magazine drum, at one scale and at game size |
| `gate1-r5-kite-compare-sheet.png` | The kite as shown in round 4 above the reworked kite |
| `gate1-r5-kite-sheet.png` | The reworked kite at eight orientations with a game-size row |
| `gate1-r5-faction-dress-sheet.png` | The repair dock in neutral, Shoal, Veil and Bastion dress |
| `gate1-r5-kite.blend` | The reworked kite |
| `gate1-r5-gimbal-shoal-dress.blend`, `gate1-r5-gimbal-veil-dress.blend`, `gate1-r5-gimbal-bastion-dress.blend` | The dressed repair docks; each faction's parts are objects named for the faction |
| `gate1-r5-stations.py`, `gate1-r5-sheet.py` | The scripts, with the dress parts and the new sheets |

## How to regenerate

```
blender -b --factory-startup --python gate1-r5-stations.py -- <renders dir> gimbal kite drum gimbal@shoal+dress gimbal@veil+dress gimbal@bastion+dress
python gate1-r5-sheet.py <renders dir> <sheets dir> family gimbal kite drum
python gate1-r5-sheet.py <renders dir> <sheets dir> stations kite
python gate1-r5-sheet.py <renders dir> <sheets dir> dress
```

The compare sheet needs the round-4 kite renders saved under the name `kite0`.

## What the pictures are and are not

- The same limits as rounds 3 and 4: Blender EEVEE previews, the same eight orientations for every shape, a downscale for game size, nothing in Unity.
- Dress adds parts and swaps six hull colours and the window light. The hull mesh and the green repair lights are the same in all four versions.
- Triangle counts: kite 4,744; dressed repair docks 6,244 to 6,688, against 4,324 undressed.
- The repair station's trigger is a sphere of radius 16 u (`Repair Station.prefab`), so the whole 29 u station sits inside it and no berth is too tight for the trigger.
