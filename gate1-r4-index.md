# Outpost kit, gate 1 round 4: mod station options and faction skins, 2026-11-14

Follows the owner's verdict on round 3: the repair dock is liked, the mod station's lantern is too close to it, and the owner wants a version of each station per faction.

| File | What it is |
|---|---|
| `gate1-r4-mod-options-sheet.png` | Three replacements for the lantern, each beside the repair dock and the armory at game size |
| `gate1-r4-clam-sheet.png`, `gate1-r4-kite-sheet.png`, `gate1-r4-diabolo-sheet.png` | Each option at eight orientations with a game-size row |
| `gate1-r4-faction-skins-sheet.png` | The repair dock in neutral, Shoal, Veil and Bastion hull colours |
| `gate1-r4-clam.blend`, `gate1-r4-kite.blend`, `gate1-r4-diabolo.blend` | The models |
| `gate1-r4-stations.py`, `gate1-r4-sheet.py` | The round-3 scripts with the new shapes, the skins and the new sheets |

## How to regenerate

```
blender -b --factory-startup --python gate1-r4-stations.py -- <renders dir> gimbal drum clam kite diabolo gimbal@shoal gimbal@veil gimbal@bastion
python gate1-r4-sheet.py <renders dir> <sheets dir> mod-options
python gate1-r4-sheet.py <renders dir> <sheets dir> stations clam kite diabolo
python gate1-r4-sheet.py <renders dir> <sheets dir> factions
```

## What the pictures are and are not

- The same limits as round 3: Blender EEVEE previews, the same eight orientations for every shape, a downscale for game size, nothing in Unity.
- A skin here changes six hull colours and the window light. The mesh and the green repair lights are the same in all four. Faction hull colours are my picks around each faction's `color` in `Settings/Factions/`, not sampled from the ship skins.
- Triangle counts: clam 4,900, kite 2,124, diabolo 1,612.
