# Nightshade

Nightshade.blend holds the editable source and packed 2048-pixel paint masks. Live symmetry, geometry and PaintUV are preserved. Concept 05 is approved; marker texture pass 03 is awaiting visual review.

The separate Shadow, Light and Ink masks carry overlapping marker-like tones, selective dark accents and drawn cockpit reflections. Emission preserves the small side and paired shoulder lights. Base coats stay recolourable through palettes.json and each material's "Base coat - recolour here" node.

The material contains no physical specular or clearcoat. "Painted-only color" exposes the paint without scene lighting; "Surface lighting response" adds a small matte diffuse contribution for source review. paint-settings.json records the controls.

paint/marker-top.png and marker-bottom.png are the registered paint sources; marker-provenance.json records their generation and transfer. Nightshade-Paint-Layers.ora exposes the grayscale layers, and UV-guide.svg shows the original charts. After editing a mask, export it over its PNG, reload and repack it in Blender, and save the source.
