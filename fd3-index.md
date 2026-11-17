# Faction dress, gate 2, 2026-11-16 (#1592)

Round 2's direction was accepted on #1592 (issuecomment-6938757598). Gate 2 adds ink weathering to the parts and a colour tuner with each faction's palette.

| File | What it is |
|---|---|
| `fd3-wear-chalk.png` | Each faction's parts at the closest zoom, round 2 beside the weathered parts |
| `fd3-factions-copper.png`, `fd3-factions-verdant.png` | Every station in neutral and in each faction's dress, at the camera's widest zoom, on two locales |
| `fd3-factions-copper-dark.png` | The same, dark |
| `fd3-close-gimbal-chalk.png`, `fd3-close-kite-chalk.png`, `fd3-close-drum-chalk.png` | Each station face on at the closest zoom, in all four dresses |
| `fd3-stations.py` | The station builder with mounts, the three part kits and their wear |
| `fd3-compose_dress.py`, `fd3-dress-colour.json` | The compositor and the one settings block with each faction's palette |
| `fd3-tuner-template.html`, `fd3-build_tuner.py` | The colour tuner page and the script that bakes it |

## What changed since round 2

- **Ink marks on the parts.** The parts take the same strokes as the hull plates: hatching, crossed hatching and single scratches, in the outline's ink. They are kept inside each face, so none hang off an edge.
- **A wear level per faction.** These levels are the art session's call, not a ruling:
  - Shoal is the most worn. It has grime on every patch and on most crates.
  - Bastion is scarred: scratches across its armour, chevrons and banners.
  - Veil is the cleanest, with a lone scratch on some cladding.
- **Layouts stay put.** The wear draws from its own seed per station and faction, so every part sits where round 2 put it.
- **One service table.** The settings block's separate `minimap` table is gone. `service` is the one table, and it already held the minimap's find colours.

## The tuner

The page shows one station at a time, or all four dresses of one station side by side at the widest zoom. The knob groups:

- **Palette.** One group edits the picked dress: three accent roles and the window light. Neutral's group edits the base accents.
- **Shared.** The station body, the service lights, shading, ink weathering and glow are shared by every dress.

"Copy settings" exports the same shape as `fd3-dress-colour.json`.

## Regenerate

```
blender -b --factory-startup --python fd3-stations.py -- <absolute passes dir> passes brush=art/ships/crimson/textures/painted-brush-source.png gimbal gimbal@shoal+kit gimbal@veil+kit gimbal@bastion+kit
python fd3-compose_dress.py <passes dir> <captures dir> <sheets dir> [<round 2 passes dir>]
python fd3-build_tuner.py <passes dir> <captures dir> fd3-dress-colour.json <out dir>
```

- Run the Blender line for `kite` and `drum` too. Give it an absolute passes dir, because Blender resolves a relative one against the drive root.
- The compositor reads `dress-colour.json` beside it, so rename `fd3-dress-colour.json` to that.
- The captures are the `*-flight.png` backdrops from gate 3.
- The tuner script reads `template.html` beside it.

## What the pictures are and are not

- These are Blender masks recoloured over game captures, not the game's renderer.
- The marks are as small as the hull's: about 1.5 px wide at the closest zoom and under a pixel at the widest. They read up close and fade into texture when the camera pulls back.
- Nothing in the game has changed.
