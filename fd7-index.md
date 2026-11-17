# Faction dress, round 6, 2026-11-16 (#1592)

The owner on round 5 (#1592, issuecomment-6940393169): "hmm I dont like the green on the veil, but bastion is good, maybe we keep both and randomize (all red or more red accents)". This round is colour only, on gate 2's passes.

| File | What it is |
|---|---|
| `fd7-factions-copper.png`, `fd7-factions-verdant.png` | Every station in every palette, at the widest zoom, on two locales |
| `fd7-factions-copper-dark.png` | The same, dark |
| `fd7-close-gimbal-chalk.png`, `fd7-close-kite-chalk.png`, `fd7-close-drum-chalk.png` | Each station face on at the closest zoom, in every palette |
| `fd7-compose_dress.py`, `fd7-dress-colour.json` | The compositor and the settings block, with palette lists |
| `fd7-tuner-template.html` | The tuner page (bake it with `fd3-build_tuner.py`) |

## What changed

- **Veil drops the green.** Its second accent is round 3's pale steel `#9AA3B3` again, so Veil is dark gunmetal and violet.
- **Bastion has two palettes.**
  - "Red hull" is round 4: crimson body with bone armour, banners and chevron plates.
  - "Bone hull" is round 5: bone body with crimson accents.
- **The settings shape.** Each faction now holds a list of palettes. Each palette has a `name`, a `hull` with the six colour roles, a `window` light, and `brush` and `hatch` strengths. Shoal and Veil list one palette, Bastion two. The kit stays one per faction, so both Bastion palettes dress the same parts.
- **The tuner** picks a dress as a faction plus one of its palettes, and "Every dress" shows all five side by side.

## Open for integration

- **When a station rolls its palette.** The art session reads "randomize" as each station rolling on its own, so one Bastion sector can show both. When the roll happens (at spawn) and from which seed is integration's call.
- **Bone stripes on the bone hull.** The armour and banner stripes are dark red on red, because one colour role paints both those stripes and the patch panels. Giving stripes a role of their own would need the station builder to tag them.

## What the pictures are and are not

- These are Blender masks recoloured over game captures, not the game's renderer.
- Nothing in the game has changed.
