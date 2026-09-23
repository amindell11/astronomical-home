# Procedural space-skybox generator

`skybox_merged.py` renders a seamless 360° equirectangular **HDR space skybox**
(volumetric nebula + procedural star field + explicit HDR anchor stars) straight
to a scene-linear file usable as a Unity `Skybox/Panoramic` texture that also
drives image-based lighting.

The shipped asset `Assets/Visuals/Environment/Sky/nebulaCustom0.hdr` was produced
by this script.

The approved glow variant is saved at
`Assets/Visuals/Environment/Sky/nebulaGlow0.hdr`: 8192x4096, 64 samples,
seed 7319, tiny-star brightness 0, anchor brightness 1, and nebula-core
emission 1.5. It retains the large focal stars and uses Unity bloom.

## Requirements

Blender 5.1+ with Cycles (OptiX/CUDA/HIP GPU auto-detected, CPU fallback). No
add-ons — everything is procedural nodes built in `bpy`.

## Usage

```bash
# Fast iteration preview (2K EXR + AgX PNG next to the script)
blender -b -P skybox_merged.py

# Shipped 8K Radiance HDR
blender -b -P skybox_merged.py -- \
  --width 8192 --height 4096 --samples 64 --format HDR --out /path/to/nebulaCustom0
```

Each run writes `<out>.<ext>` (EXR or HDR), `<out>_preview.png` (AgX tonemapped),
and `<out>_report.txt` (resolution, render time, max radiance, % HDR pixels). The
report measures radiance above 1.0; dark settings may contain none.

## Parameters (after `--`)

| Flag | Default | Notes |
|------|---------|-------|
| `--width` / `--height` | 2048 / 1024 | Equirectangular, keep 2:1. |
| `--samples` | 32 | Cycles adaptive samples (no denoise, to avoid panorama-seam artifacts). |
| `--format` | EXR | `EXR` (32-bit float) or `HDR` (Radiance RGBE). |
| `--seed` | 7319 | Varies the sparse-star layer; the nebula palette/structure are seed-extendable for per-sector variety. |
| `--star-brightness` | 1 | Multiplier for both tiny-star layers; `0` removes the small star field. |
| `--anchor-brightness` | 1 | Independent multiplier for the five bright focal stars; `0` omits their geometry. |
| `--nebula-core-emission` | 1 | Emission multiplier at maximum density and palette luminance, tapering to 1 in dim or wispy gas. `1.5` boosts dense cores by up to 50% without changing density or the dark-space wash. |
| `--out` | script dir | Output basename (extension added automatically). |

Brightness/emission controls must be finite and non-negative. Their defaults
preserve the original output. For a completely starless sky, set both
`--star-brightness 0 --anchor-brightness 0`.

```bash
# Keep bright focal stars, remove tiny stars, and gently boost nebula cores.
blender -b -P skybox_merged.py -- \
  --star-brightness 0 --anchor-brightness 1 --nebula-core-emission 1.5 \
  --format HDR --out /path/to/nebula-glow
```

The report records all three controls. Emission changes also change sky-derived
lighting. The HDR output has no baked bloom; compare through Unity's existing
bloom at fixed exposure and quality, including ship lighting and reflections.

## Design notes

- Stars and the deep-space wash are sampled in **direction space** on the World
  shader, so there is no 0/360 seam and no pole singularity by construction.
- The nebula is a real Principled Volume in a domain cube (object-space 3D noise
  drives density; emission is tied to density so dense cores glow above 1.0),
  captured by an equirectangular panoramic camera at the origin — genuine HDR
  radiance for IBL, not a tonemapped background.
