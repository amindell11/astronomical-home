# Nightshade

Nightshade.blend holds the editable source and packed 2048-pixel paint masks. Live symmetry and modifiers remain editable. Concept 05 is approved; texture pass 02 is awaiting visual review.

The four raw masks in layers/ carry Shadow, Light, Ink and Emission. They hold panel-plane shading, narrow edge accents, painted canopy reflections and the small green lights. Base coats remain recolourable through palettes.json and each material's "Base coat - recolour here" node. paint-settings.json records blend strengths and per-region highlight colors.

The "Painted-only color" shader exposes the composite without scene lighting. The saved surface mixes in 12% material lighting response. Both views are captured for review.

Nightshade-Paint-Layers.ora exposes the masks as named layers; UV-guide.svg shows their charts. Export an edited layer over its matching PNG, reload and repack it in Blender, then save the source.
