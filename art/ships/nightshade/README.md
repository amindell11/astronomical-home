# Nightshade

Nightshade.blend holds the editable source and packed 4096-pixel paint masks. The geometry and live X mirrors remain editable. Concept 05 is approved; asymmetric marker texture pass 04 is awaiting visual review.

PaintUV gives native and mirrored surfaces independent charts in the lower-left and lower-right atlas quadrants. The upper half is reserved. Half-scale charts and a 0.5 U offset on the live Mirror modifiers preserve the prior texel density while allowing asymmetric painted lighting.

Shadow, Light and Ink carry overlapping marker tones, selective dark accents and drawn cockpit reflections. Emission places three small lights on each visible side louver bank and retains the paired shoulder indicators. Base coats stay recolourable through palettes.json and each material's "Base coat - recolour here" node.

The material contains no physical specular or clearcoat. "Painted-only color" exposes the paint without scene lighting; "Surface lighting response" adds a small matte diffuse contribution for source review. paint-settings.json records the controls.

paint/marker-top.png and marker-bottom.png are the paint sources; marker-provenance.json records generation, transfer and the light cleanup. Nightshade-Paint-Layers.ora exposes the grayscale layers, and UV-guide.svg shows both sets of charts. After editing a mask, export it over its PNG, reload and repack it in Blender, and save the source.
