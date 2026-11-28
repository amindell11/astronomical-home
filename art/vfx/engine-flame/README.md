# Engine flame

The main exhaust flame: a short, cel-banded flame in the style of the Bulwark ram and
Vanguard dash flames, but more subdued, with a soft glow. One band-mask drawing serves
every faction: the flame material picks the three band colours from its base colour.
Design rulings are on [#1785](https://github.com/amindell11/astronomical-home/issues/1785).

## Files

| File | Role |
|---|---|
| `tuner.template.html` | The generator, with a slider per knob, previews on a hull and at game size, and an export block. It is also what `bake.js` draws with. |
| `settings.json` | The knob values arye exported from the tuner (2026-11-28). `pilotLength` (idle flame size) is runtime-only and has no tuner slider yet. |
| `bake.js` | Renders the flipbook from the template and settings. |

Sketch rounds, image-gen rounds (rejected: too many notches) and the on-ship mocks live
on the `evidence/engine-look` branch under `history/`.

## Regenerate

```bash
node art/vfx/engine-flame/bake.js src/Asteroids3D/Assets/Visuals/Vfx/EngineFlame/Textures
```

It needs Playwright and its Chromium. It writes:

- `EngineFlame_Bands.png`: 4×2 flipbook, 128×320 per frame, nozzle at the top. R, G and B
  are the rim, mid and core band coverage, nested (rim ⊇ mid ⊇ core).
- `EngineFlame_Glow.png`: the same layout, holding a blurred copy of the rim coverage.
- `values.json`: the material and motion values the textures were baked for.

Length and width wobble are baked into the drawings. Frame choice, ignition growth,
cutoff shrink, the idle pilot flame and throttle length happen at runtime.

To publish the tuner, put `settings.json` into the `DEF` block between `/*SETTINGS*/` and
`/*END*/`, then publish the file with the Artifact tool.
