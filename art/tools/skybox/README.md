# Procedural space-skybox generator

`skybox_merged.py` renders a seamless 360° equirectangular **HDR space skybox**
(volumetric nebula + procedural star field + explicit HDR anchor stars) straight
to a scene-linear file usable as a Unity `Skybox/Panoramic` texture that also
drives image-based lighting.

The shipped asset `Assets/Visuals/Environment/Sky/nebulaCustom0.hdr` was produced
by this script.

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
render self-validates that the output actually contains >1.0 radiance.

## Parameters (after `--`)

| Flag | Default | Notes |
|------|---------|-------|
| `--width` / `--height` | 2048 / 1024 | Equirectangular, keep 2:1. |
| `--samples` | 32 | Cycles adaptive samples (no denoise, to avoid panorama-seam artifacts). |
| `--format` | EXR | `EXR` (32-bit float) or `HDR` (Radiance RGBE). |
| `--seed` | 7319 | Varies the sparse-star layer; the nebula palette/structure are seed-extendable for per-sector variety. |
| `--out` | script dir | Output basename (extension added automatically). |

## Design notes

- Stars and the deep-space wash are sampled in **direction space** on the World
  shader, so there is no 0/360 seam and no pole singularity by construction.
- The nebula is a real Principled Volume in a domain cube (object-space 3D noise
  drives density; emission is tied to density so dense cores glow above 1.0),
  captured by an equirectangular panoramic camera at the origin — genuine HDR
  radiance for IBL, not a tonemapped background.
