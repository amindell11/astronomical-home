# Keystone trails

The keystone effects (Dash, Ram, and later Overdrive and Blink): angular, hand-inked streaks that peel off the hull while a keystone is active. They're shed by `KeystoneTrail` on each kit prefab and drawn by the `Keystone Streak` shader from `Assets/Visuals/Vfx/KeystoneTrails/`. The design record and the owner's rulings are on [#1574](https://github.com/amindell11/astronomical-home/issues/1574). The concept rounds, the cleanup sources and the parse script are on the `evidence/keystone-trails` branch under `history/`.

## Files

| File | Role |
|---|---|
| `tuner/data.json` | The cleaned-up concept frames parsed into streak polygons, plus the ship sprites with hull outlines and the backdrop crop. This is the generator's source; the parse that made it is in the history. |
| `tuner/picks.js` | The owner's tuned settings for each ship. The Unity particle systems take their numbers from here. |
| `tuner/script.js`, `tuner/template_head.html` | The browser tuner: the hull-shedding sim, the knobs, the starting points and the blends. |
| `tuner/build_page.py` | Assembles the tuner into `tuner/out/keystone_trails.html`. |
| `bake/bake_atlas.py` | Rasterises the streak polygons into the shader's atlas, and draws the bow-shock arcs. |

## Regenerate

```bash
cd art/vfx/keystone-trails
python tuner/build_page.py        # tuner page; publish it with the Artifact tool
uv run bake/bake_atlas.py --export ../../../src/Asteroids3D/Assets/Visuals/Vfx/KeystoneTrails/Textures
```

Atlas layout (8 × 4 cells, streaks point up, head at the top): body streaks 0–15, bow-shock streaks 16–23, speed lines 24–27, motes 28–29, star 30. The particle systems pick their cells by frame range, so change the layout and the prefabs together. Channels: R coverage, G position along the streak (1 at the head), B distance inside the edge.

## Mapping the tuner to Unity

| Tuner knob | Unity |
|---|---|
| Rate over distance, hull region, inherit, kick, push-off burst, start size | `HullShed` fields on `KeystoneTrail` |
| Lifetime, size over lifetime, collapse, drag | the particle system's main, size-over-lifetime and limit-velocity (drag) modules |
| Stretch with speed | stretched-billboard speed scale |
| Fill, fade toward tail, ink, hatching, light blend, hot core | `Keystone Streak` material |
| Tone bands, ship ink (the "House style" blend) | `_Bands` and `_InkColor` on the material |

Not carried over: line boil, animating on twos, the end break at the window's end, and the Bent space, Cools into space and Lit hull blends.
