# Station paint tools

Bake the outpost stations' brush texture and hatching into the drawn shader's paint masks
(`_PaintShadowMap`, `_PaintLightMap`, `_PaintInkMap`, read through UV0). Each station gets its
own UV layout, saved in its `.blend`, and its own mask set.

Hull meshes are the mesh objects whose `key` custom property is a hull colour: `bone`, `canvas`,
`slate`, `ochre`, `brick`, `blue`. Everything else is left alone.

Both tools run headless in Blender 5.1:

```
blender -b --factory-startup --python-exit-code 1 -P art/tools/station/station_uv.py -- \
  --station art/stations/<station>/<station>.blend [--redo]
blender -b --factory-startup --python-exit-code 1 -P art/tools/station/station_paint.py -- \
  --station art/stations/<station>/<station>.blend --out <dir> [--unity <dir>]
```

## station_uv

Adds a `PaintUV` layer to every hull mesh and saves the `.blend` in place; it is the only tool
that writes the source. All hull meshes share one 0-1 atlas at one texel density (angle-based
islands, at least 8 texels apart at 2048). Refuses a station that already has `PaintUV` unless
`--redo`. Reports texels per unit at 2048, atlas fill, per-face density max/min and island count
as stdout trailers (`STATION_TEXELS_PER_UNIT`, `STATION_FILL`, `STATION_DENSITY_RATIO`,
`STATION_ISLANDS`).

## station_paint

Never saves the `.blend`. Bakes (Cycles, emission, via `ship_paint.bake`) the outpost preview's
brush and hatch node setups, in each mesh's Object coordinates, onto `PaintUV` at 2048, then writes
8-bit grey masks to `layers/` beside the source:

| Mask | Value |
|---|---|
| `Shadow.png` | `clamp(3.2 * (0.625 - brush), 0, 1)` |
| `Light.png` | `clamp(3.2 * (brush - 0.625), 0, 1)` |
| `Ink.png` | `(hatch red + hatch green) / 2`: 0.5 on one stroke set, 1 where the sets cross |

Texels away from any island stay 0. `--unity <dir>` also writes the three masks into an existing
folder, overwriting in place (Unity owns the `.meta` files). The review image
`<out>/<station>-flat.png` shows the station from the top
and tilted, shaded the way the shader composes the masks with the colours in
`art/stations/outpost-colour.json`. Stdout trailers: `STATION_MASKS=<layers dir>`,
`STATION_PREVIEW=<png>`.

## Strengths live on the material

The masks carry no strength, so the look can be tuned in Unity without a rebake. From
`outpost-colour.json`:

| Material field | Value |
|---|---|
| `_PaintShadowStrength` | `brush` |
| `_PaintLightStrength` | `brush` |
| `_PaintInkStrength` | `2 * hatch` |
| `_PaintLightColor` | `2 * base`, in linear, each channel clamped to 1 |

## Contract

Exit 0 written, 1 error (bad arguments, missing input, refusal, crash). Prose goes to stderr;
stdout carries only the trailers, written last.
