# Vanguard livery UV guides

These guides use the `LiveryUV` map in
`../vanguard.blend`. The original `UVMap` remains
unchanged and remains the render-active UV map.

## Guide files

- `../textures/livery.svg` — master UV artwork with an editable paint layer.
- `vanguard_livery_uv_centerbody.svg` — `Fuselage` and `Cockpit.001`.
- `vanguard_livery_uv_wings.svg` — `Wing` and `wing_tail`.
- `vanguard_livery_uv_tail.svg` — `Sparrow Tail` and `Tail`.
- Matching PNG files are quick-view references; use the SVG files for editing.

Every guide uses the same 2048×2048 canvas and atlas coordinates. Keep their
canvas size and positions unchanged when combining them in a vector editor.

## Painting workflow

1. Open `../textures/livery.svg` in a vector editor.
2. Keep the UV artwork on a locked guide layer.
3. Add separate livery layers for center body, wings, and tail.
4. Draw closed white vector shapes for orange paint over a black background.
5. Export the finished mask as `../textures/livery_mask.png`, at 2048×2048.
6. In Blender, use a `UV Map` shader node set to `LiveryUV` to drive the mask
   image's Vector input.
7. Set the mask image to `Non-Color`, then connect its Color output through a
   Color Ramp to the hull-paint Mix factor.

The modeled left/right halves come from Mirror modifiers and intentionally
share UV coordinates. A shape painted on one side therefore appears
symmetrically on the other side.

The atlas is normalized to approximately 1203 pixels per meter at 2048×2048.

These guides date from July 26. The active PNG was edited later than the SVG;
keep it until you have compared the artwork with your current UVs. The master
guide PNG here is an outline preview, not the active livery mask.
