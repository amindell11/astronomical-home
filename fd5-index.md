# Faction dress, round 4, 2026-11-16 (#1592)

The owner set the direction on #1592 (issuecomment-6940065325): Bastion moves toward crimson and bone, matching the proposed Bastion ships on #1320's board. Veil keeps its dark gunmetal and violet, with green worked in. Shoal and neutral are unchanged from round 3. This round is colour only, on gate 2's passes.

| File | What it is |
|---|---|
| `fd5-compare-copper.png` | One station per faction: neutral, round 3 and round 4, at the widest zoom |
| `fd5-factions-copper.png`, `fd5-factions-verdant.png` | Every station in every dress, at the widest zoom, on two locales |
| `fd5-factions-copper-dark.png` | The same, dark |
| `fd5-close-gimbal-chalk.png`, `fd5-close-kite-chalk.png`, `fd5-close-drum-chalk.png` | Each station face on at the closest zoom, in all four dresses |
| `fd5-compose_dress.py`, `fd5-dress-colour.json` | The compositor and the settings block |
| `fd5-tuner-template.html` | The tuner page (bake it with `fd3-build_tuner.py`) |

## What changed

| Faction | Body | Accents | Windows | Texture |
|---|---|---|---|---|
| Bastion | Crimson `#7A2734`, deep crimson `#561A26`, near-black frame `#241F22` | Bone `#EBCB9E` (armour, banners, chevron plates), dark crimson `#4A1520` (stripes and chevrons), aged bone `#B8A07E` | Cold white `#D7E6FF` | Brush 0.7, hatching 0.14 |
| Veil | As round 3 | The pale steel accent becomes muted acid green `#56802A` | Violet `#BA82FF` | As round 3 |

- **Where the colours come from.** Bastion's crimson and bone were sampled from the proposed Bastion ships, a little lighter so they survive the station's shading.
- **Veil's green is paint only**, so it never glows near the repair dock's teal light.
- **Veil's green, first try.** At `#8FD13A` the green filled whole panels (the mod station's cargo block, the armory's top magazine) and fought the violet. Muted, it sits under the violet.

## What the pictures are and are not

- These are Blender masks recoloured over game captures, not the game's renderer.
- Nothing in the game has changed.
