# Faction dress, round 2, 2026-11-16 (#1592)

Built from the owner's picks on round 1 (#1592, issuecomment-6936500756): louder parts, violet window light in Veil space, and service lights in the minimap's find colours.

| File | What it is |
|---|---|
| `fd2-factions-copper.png`, `fd2-factions-verdant.png` | Every station in neutral and in each faction's dress, at the camera's widest zoom, on two locales |
| `fd2-factions-copper-dark.png` | The same, dark |
| `fd2-close-gimbal-chalk.png`, `fd2-close-kite-chalk.png`, `fd2-close-drum-chalk.png` | Each station face on at the closest zoom, in all four dresses |
| `fd2-stations.py`, `fd2-compose_dress.py`, `fd2-dress-colour.json` | Builder, compositor and settings block. Regenerate as in `fd1-index.md` |

## What changed from round 1

- **Louder kits: fewer, bigger parts that break the outline.**
  - Shoal: double-layer scrap patches, bigger cargo stacks, lamp strings on 2.4 u poles, lantern masts with a crossbar, and one salvaged dish per station.
  - Veil: four sensor masts of 5.5 to 8 u with lit crossbars, bigger cladding with two lit seams, heavier cables and heat sinks.
  - Bastion: four 6 u standards with a 2.2 by 3.6 u banner, heavier armour pairs, larger chevrons.
- **Nothing tall in the berth.** The repair dock's inner ring faces into the berth, so its mounts take only flat parts. Masts, poles, strings and dishes go elsewhere. A mount carries this as `tall`.
- **Veil's window light is violet `#BA82FF`.** It is a touch redder than the `#B48CFF` the owner picked from, to stay clear of the armory's new `#8C9CFF`.
- **Service lights in `FindColours`.** Repair `#5FE0C0`, mod `#FFD27A`, armory `#8C9CFF`, in every dress.
- **Palettes.** Veil's third accent changed from olive to gunmetal blue `#4D5868`. Bastion's white is toned to `#CFD8E6`.

## What the pictures are and are not

The same as round 1. These are Blender passes recoloured over game captures, the parts carry no ink marks yet, and each station is sized without its dress.
