# Nightshade

Nightshade.blend holds the editable source and packed 2048-pixel paint masks. Live symmetry and modifiers remain editable. Concept 05 is approved; the first texture pass is awaiting visual review.

The four raw masks in layers/ carry Shadow, Light, Ink and Emission. Base coats remain recolourable through palettes.json and each material's "Base coat - recolour here" node. paint-settings.json records their blend strengths.

Nightshade-Paint-Layers.ora exposes the masks as named layers; UV-guide.svg shows their charts. Export an edited layer over its matching PNG, reload and repack it in Blender, then save the source.
