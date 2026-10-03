# Editable Valis paint prototype

This is a Blender texture-authoring prototype, not the finished Unity material pass. The approved geometry, separate parts and live mirrors are unchanged. Jade + iris is saved as the default; the alternate renders use the same masks with different base colors.

## Hand edits

Open `Valis-painted-v2.blend`. Every material has a named **Base coat - recolor here** color node and three clearly labelled image nodes. The shared `ValisPaintUV` atlas is 4096 square; `UV_Layout.png` is a layout guide.

- `Paint_Shadow.png`: brush variation and painted shade. White adds dark paint; black removes it.
- `Paint_Light.png`: brush highlights, rim highlights and canopy reflections. White adds light paint; black removes it.
- `Paint_Ink.png`: panel borders and the sparse technical details. White adds ink; black removes it.

Use Blender's Texture Paint workspace and choose the image you want to paint, or edit the grayscale PNG in an image editor. In Blender, save changed images and save the blend. Images are packed as well as supplied externally; after external edits, reload the images and repack them before saving the blend. Keep this folder together so the relative PNG paths continue to resolve.

Base colors and layer strengths are separate controls in the Shader Editor. Mirrored parts share paint UVs. An asymmetrical paint pass would need separate mirrored UV islands, without changing geometry.

The draft generators live separately in review evidence. Do not regenerate over hand edits: this blend and its edited PNGs become the working source, and any further generated candidate goes into a new version folder.

`geometry-verification.json` records the comparison against the approved 36-part source. `quarter.png`, `top.png`, and `game-scale.png` are actual-model Blender renders; `ivory-lavender.png` and `jade-gray.png` demonstrate recoloring with the same paint masks.
