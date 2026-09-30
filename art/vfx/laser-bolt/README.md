# Laser bolt

The base laser's projectile visual: a short, cel-banded blue bolt whose torn edges
crackle by changing shape. Shipped on `Prefabs/Weapons/Laser.prefab` (child `Bolt`),
from `Assets/Visuals/Vfx/LaserBolt/`. Worked example for the `aesthetic-authoring`
skill; the rejected approaches and their reasons are in the PR that shipped it.

## Files, in the order they were made

| Stage | Files |
|---|---|
| References | `refs/`: the pick from the concept sketch, the drawn explosion sheet, the game style frame. The main inspiration, a Star Wars blaster-bolt still (thick white core, thin blue sheath, soft bloom), is left out of git. |
| Concept sketch | `svg-concept/`: `gen.py` draws band, fringe, glow, muzzle, impact and spark layers in two colourways. The spark layer became the pick. The long thin streak is a candidate for the railgun beam. |
| Imagegen | `bolt_refine*.jpg` + sidecars + prompts. Round 1 asked for a long streak; round 2 (short bolt, torn band edges, no floating filaments) gave `bolt_refine_r2_pro.jpg`. |
| Hand cleanup | `bolt_flat.webp`: the user's flattened version of r2 pro. The generator's source. |
| Generator | `crackle/crackle.py`: splits the source into rim, mid, hot and core bands and warps each band's edge per frame. The constants at the top are the knobs. |
| Tuner | `crackle/tuner.template.html`: the generator ported to the browser with a slider per knob. |

## Regenerate

```bash
cd art/vfx/laser-bolt/crackle
python crackle.py                                   # previews in out/
python crackle.py --tuner out/tuner.html            # tuner page; publish it with the Artifact tool
python crackle.py --export ../../../../src/Asteroids3D/Assets/Visuals/Vfx/LaserBolt/Textures
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
