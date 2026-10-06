# Crimson

Open `Crimson.blend` for the approved editable ship and first hand-painted texture
pass. The 39 mesh parts stay separate, with 33 live Mirror modifiers centered on
the ship. The original design references and the color atlas
are packed into the file; external copies are included for editing.

The Blender source uses X across the wings, +Y toward the nose and +Z up.

## Texture editing

The `PaintUV` map uses `textures/Crimson_BaseColor.png`, a 4096×4096 atlas.
Mirrored parts share their paint. Select the atlas in Blender's Texture Paint
workspace to edit it. Save the external image and repack it when saving the blend.
The saved solid viewport uses Texture color and Flat lighting to show the painted
highlights and shadows. The material also references the atlas for base color.

`reference/approved-texture-concept.png` is the approved hand-drawn direction:
silver armor, red insets, charcoal structure, dark outlines and broad painted
light and shadow shapes. `textures/painted-brush-source.png` supplies the imagegen
brush variation; its exact prompt is alongside it. Generated concepts guide the
paint treatment; the Blender geometry is authoritative.

## Review evidence

![Textured ship](previews/textured-hero.png)

![Original AI mesh and rebuilt ship rotating together](previews/old-vs-new-turntable.gif)

- [Full-resolution turntable video](previews/old-vs-new-turntable.mp4), original AI
  mesh on the left and rebuilt ship on the right, matched by nose-to-tail length.
- [Top](previews/textured-top.png), [side](previews/textured-side.png),
  [front](previews/textured-front.png) and [game-scale](previews/textured-game-scale.png)
  texture checks.
- `orthographic/` contains untextured geometry captures.
- `asset-validation.json` records mesh, texture and FBX round-trip checks.

The approved source retains 14 non-manifold edges across `Cube` and `Structural
center web`; no faces have zero area. These authored parts are preserved in the
render mesh rather than repaired as part of the asset handoff.

These are Blender previews of the asset; this handoff does not replace the active
gameplay ship or claim an in-engine rendering match.
