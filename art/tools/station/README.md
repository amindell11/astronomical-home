# Station paint tools

Bake the outpost stations' brush texture, hatching and hash marks into the drawn shader's paint masks
(`_PaintShadowMap`, `_PaintLightMap`, `_PaintInkMap`, read through UV0). Each station gets its
own UV layout, saved in its `.blend`, and its own mask set.

Hull meshes are the mesh objects whose `key` custom property is a hull colour: `bone`, `canvas`,
`slate`, `ochre`, `brick`, `blue`. Everything else is left alone.

Both tools run headless in Blender 5.1:

```
blender -b --factory-startup --python-exit-code 1 -P art/tools/station/station_uv.py -- \
  --station art/stations/<station>/<station>.blend [--redo]
blender -b --factory-startup --python-exit-code 1 -P art/tools/station/station_paint.py -- \
  --station art/stations/<station>/<station>.blend --out <dir> [--unity <dir>] [--hatch <strength>]
```

A faction's dressed station has its own folder, `art/stations/<station>/<faction>/`, holding
`<station>-<faction>.blend` and its `layers/`, and runs through both tools like the plain one.

## station_uv

Adds a `PaintUV` layer to every hull mesh and saves the `.blend` in place; it is the only tool
that writes the source. All hull meshes share one 0-1 atlas at one texel density (angle-based
islands, at least 8 texels apart at 2048). Refuses a station that already has `PaintUV` unless
`--redo`. Reports texels per unit at 2048, atlas fill, per-face density max/min and island count
as stdout trailers (`STATION_TEXELS_PER_UNIT`, `STATION_FILL`, `STATION_DENSITY_RATIO`,
`STATION_ISLANDS`).

## station_paint

Never saves the `.blend`. Bakes (Cycles, emission, via `ship_paint.bake`) the outpost preview's
brush and hatch node setups, in each mesh's Object coordinates, onto `PaintUV` at 2048. The hash
marks (meshes keyed `mark`) bake onto the hull in world space, selected to active, at 4096 and
filtered down to 2048. Then it writes 8-bit grey masks to `layers/` beside the source:

| Mask | Value |
|---|---|
| `Shadow.png` | `clamp(3.2 * (0.625 - brush), 0, 1)` |
| `Light.png` | `clamp(3.2 * (brush - 0.625), 0, 1)` |
| `Ink.png` | `max(hatch * (hatch red + hatch green), marks)`: `hatch` on one stroke set, twice that where the sets cross, 1 on a mark |

Texels away from any island stay 0. `--unity <dir>` also writes the three masks into an existing
folder, overwriting in place (Unity owns the `.meta` files). The review image
`<out>/<station>-flat.png` shows the station from the top
and tilted, shaded the way the shader composes the masks with the colours in the nearest
`outpost-colour.json` above the source. Stdout trailers: `STATION_MASKS=<layers dir>`,
`STATION_PREVIEW=<png>`.

## Strengths

The brush masks carry no strength, so the brush can be tuned in Unity without a rebake. The Ink
mask carries the hatch strength, because the marks need full ink from the same mask; changing
`hatch` means a rebake. So the hatch belongs to the baked model: a dressed station bakes at its
faction's strength with `--hatch` (Shoal 0.24, Veil 0.08, Bastion 0.14). From `outpost-colour.json`:

| Material field | Value |
|---|---|
| `_PaintShadowStrength` | `brush` |
| `_PaintLightStrength` | `brush` |
| `_PaintInkStrength` | `1` |
| `_PaintLightColor` | `2 * base`, in linear, each channel clamped to 1 |

## Contract

Exit 0 written, 1 error (bad arguments, missing input, refusal, crash). Prose goes to stderr;
stdout carries only the trailers, written last.
