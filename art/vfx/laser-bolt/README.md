# Laser bolt

The base laser's projectile visual: a short, cel-banded blue bolt whose torn edges
crackle by changing shape. Shipped on `Prefabs/Weapons/Projectiles/Laser.prefab` (child `Bolt`),
from `Assets/Visuals/Vfx/LaserBolt/`. Worked example for the `aesthetic-authoring`
skill; the rejected approaches and their reasons are in the PR that shipped it,
[#767](https://github.com/amindell11/astronomical-home/pull/767).

## Files

| File | Role |
|---|---|
| `bolt_flat.webp` | The user's flattened cleanup of the favourite imagegen bolt. The generator's source. |
| `refs/style_frame.webp` | The game style frame; backdrop for the game-scale previews and the tuner. |
| `crackle/crackle.py` | Splits the source into rim, mid, hot and core bands and warps each band's edge per frame. The constants at the top are the knobs. |
| `crackle/tuner.template.html` | The generator ported to the browser, a slider per knob. |

The steps before the source (references, the SVG concept sketch, both imagegen rounds
with prompts and sidecars) live on the `evidence/laser-bolt` branch:
[design history](https://github.com/amindell11/astronomical-home/tree/fceab1bf6d12a858a5e0c0c2f4dd22a43b34d36d/history). The sketch's long thin streak is a candidate for the railgun beam.

## Regenerate

```bash
cd art/vfx/laser-bolt/crackle
uv run crackle.py                                   # previews in out/
uv run crackle.py --tuner out/tuner.html            # tuner page; publish it with the Artifact tool
uv run crackle.py --export ../../../../src/Asteroids3D/Assets/Visuals/Vfx/LaserBolt/Textures
```

The export prints the material values the textures need (`_Frames`, `_Fps`,
`_Crossfade`, `_Ease`); set them on `LaserBolt.mat` when the frame count or smoothing
mode changes. The tuner's seed picks different frames from the script's (different
random generators); every other knob matches.

## In Unity

`Crackle Flipbook` shader: one row of frames with the head pointing up (+V), played
over time with an eased crossfade; the glow texture adds under the body. `_BaseColor`
alpha carries `LaserVisual`'s distance fade. Every bolt crackles in step: no per-bolt
phase yet.
