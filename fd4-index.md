# Faction dress, round 3, 2026-11-16 (#1592)

On seeing gate 2, the owner ruled that a faction recolours the whole station, not only its accents and lights (#1592, issuecomment-6939691266). Round 3 gives each faction a body palette and a texture strength. Nothing was re-rendered: the passes and parts are gate 2's (`fd3-stations.py`), recoloured.

| File | What it is |
|---|---|
| `fd4-compare-copper.png` | One station per faction: neutral, gate 2's accents only, and the whole station, at the widest zoom |
| `fd4-factions-copper.png`, `fd4-factions-verdant.png` | Every station in every dress, at the widest zoom, on two locales |
| `fd4-factions-copper-dark.png` | The same, dark |
| `fd4-close-gimbal-chalk.png`, `fd4-close-kite-chalk.png`, `fd4-close-drum-chalk.png` | Each station face on at the closest zoom, in all four dresses |
| `fd4-compose_dress.py`, `fd4-dress-colour.json` | The compositor and the settings block, now with a body palette and texture per faction |
| `fd4-tuner-template.html` | The tuner page with body, accent and texture knobs per dress (bake it with `fd3-build_tuner.py`) |

## Each faction's station

The art session's proposal, open to tuning:

| Faction | Body | Accents | Windows | Texture |
|---|---|---|---|---|
| Shoal | Rust `#A8714A`, dark rust `#6E4B36`, soot frame `#261E1B` | Orange `#E8862A`, pale sand `#D8C29A`, oxidised green `#5F7468` | `#FF9548` | Grimy: brush 1.2, hatching 0.24 |
| Veil | Gunmetal `#525864`, dark gunmetal `#3A3F4A`, black frame `#111216` | Near-black panels `#2B2F37`, pale steel `#9AA3B3`, blue-black `#3D4866` | Violet `#BA82FF` | Sleek: brush 0.45, hatching 0.08 |
| Bastion | Cold white `#E8ECF2`, pale steel blue `#AAB6C8`, navy frame `#1C2846` | Cobalt `#2E62C9`, dark navy `#14214A`, deep blue `#1D3C88` | `#D7E6FF` | Clean: brush 0.7, hatching 0.12 |

Neutral keeps the accepted look. Service lights stay one colour per kind everywhere.

## Regenerate

```
python fd4-compose_dress.py <gate 2 passes dir> <captures dir> <sheets dir> [fd3-dress-colour.json]
```

The compositor reads `dress-colour.json` beside it, so rename `fd4-dress-colour.json` to that. Passing gate 2's settings block adds the comparison sheet.

## What the pictures are and are not

- These are Blender masks recoloured over game captures, not the game's renderer.
- Veil's dark body is the hardest to pick out against black space. Its violet lights and the lit edges carry its outline there.
- Nothing in the game has changed.
