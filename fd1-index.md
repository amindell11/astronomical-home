# Faction dress, round 1, 2026-11-16 (#1592)

The first look at the shape ruled on #1381 (issuecomment-6935568278): neutral crews, faction accents and parts on the accepted hull, one service colour per kind, and one palette and part kit per faction.

| File | What it is |
|---|---|
| `fd1-factions-copper.png`, `fd1-factions-verdant.png` | Every station in neutral and in each faction's dress, at the camera's widest zoom, on two locales |
| `fd1-factions-copper-dark.png` | The same, dark |
| `fd1-close-gimbal-chalk.png`, `fd1-close-kite-chalk.png`, `fd1-close-drum-chalk.png` | Each station face on at the closest zoom, in all four dresses |
| `fd1-veil-windows.png` | Veil space with three window lights: acid, violet, ice |
| `fd1-service-colours.png` | The service lights as accepted against the minimap's find colours |
| `fd1-stations.py` | The station builder with mounts and the three part kits |
| `fd1-compose_dress.py`, `fd1-dress-colour.json` | The compositor and the one settings block with each faction's palette |

## How it's built

- **Mounts.** The builder records a mount on every free plate face as it builds: a centre, the face's long axis and its outward normal, and the part it rides on. Plated arcs get them automatically. The armory's magazines and the mod station's spine get them by hand. Gimbal 20, mod station 14, armory about 48.
- **Kits.** `kit_shoal`, `kit_veil` and `kit_bastion` read only the mounts, so each works on any station. Each picks mounts spread apart from a fixed seed per station and faction. Bastion picks its armour in mirrored pairs.
  - Shoal: patches welded on askew with rivets, strapped cargo, lamp strings between neighbouring plates, lantern poles.
  - Veil: faceted cladding with a lit seam, sensor masts, cable runs, heat-sink fins.
  - Bastion: armour with a clan stripe, standards on masts, painted chevrons.
- **Palette.** A faction recolours the accent roles (ochre, brick, blue) and the window light. Bone, canvas and slate stay as accepted. Service lights don't change.
- **Size.** Each station is sized without its dress, so no faction changes a station's scale.

## Regenerate

```
blender -b --factory-startup --python fd1-stations.py -- <passes dir> passes brush=art/ships/crimson/textures/painted-brush-source.png gimbal gimbal@shoal+kit gimbal@veil+kit gimbal@bastion+kit
python fd1-compose_dress.py <passes dir> <captures dir> <sheets dir>
```

Run the first line for `kite` and `drum` too. The compositor reads `dress-colour.json` beside it, so rename `fd1-dress-colour.json` to that. The captures are the `*-flight.png` backdrops from gate 3.

## What the pictures are and are not

- They use the same pass renders and compositor as gate 3 and the detail pass: Blender masks recoloured over game captures, not the game's renderer.
- The parts carry no ink marks yet. Hatching and brush reach them through the object-space masks.
- The faction palettes are a first proposal. The settings block holds them, so a tuner page can take them later.
