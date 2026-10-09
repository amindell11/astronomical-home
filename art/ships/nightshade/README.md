# Nightshade

Nightshade.blend holds the editable source and packed 4096-pixel paint masks. The geometry and live X mirrors remain editable. Concept 05 is approved. Texture repair 05 removes projected outline streaks on exposed wing and fin surfaces and recalibrates tail highlight masks for the current material assignments. Owner geometry, palette edits and the shared saturation control are preserved.

The Sub-assemblies collection contains nine ASSEMBLY controls: Cockpit, Main wings, Upper fins, Lower fins, Tail, Upper spine, Lower spine, Engine and Central hull. Select a control and use G, R or S to manipulate its complete assembly. Expand it to edit individual meshes. Each control carries its own mirror plane, and the panel joint beds follow their matching assembly. Export roles remain in a separate collection tree.

PaintUV gives native and mirrored surfaces independent charts in the lower-left and lower-right atlas quadrants. The upper half is reserved. Half-scale charts and a 0.5 U offset on the live Mirror modifiers preserve the prior texel density while allowing asymmetric painted lighting.

Shadow, Light and Ink carry overlapping marker tones, selective dark accents and drawn cockpit reflections. Emission places three small lights on each visible side louver bank and retains the paired shoulder indicators. Base coats stay recolourable through palettes.json and each material's "Base coat - recolour here" node.

The shared "Nightshade Palette Saturation" node group adjusts all six materials after their paint and indicator colors are combined. Select "GLOBAL SATURATION - Tab to edit" in any material and press Tab; its single Saturation slider controls the whole palette. The saved setting is 1.25. 1 preserves the authored colors, 0 is grayscale, and values above 1 increase saturation. Keep this shared group when repairing the textures.

The material contains no physical specular or clearcoat. "Painted-only color" exposes the paint without scene lighting; "Surface lighting response" adds a small matte diffuse contribution for source review. paint-settings.json records the controls.

paint/marker-top.png and marker-bottom.png are the paint sources; marker-provenance.json records generation, transfer and the light cleanup. Nightshade-Paint-Layers.ora exposes the grayscale layers, and UV-guide.svg shows both sets of charts. After editing a mask, export it over its PNG, reload and repack it in Blender, and save the source.

The Unity version uses Nightshade.fbx with Hull, Canopy, Cores and Collider roles. Its materials retain the grayscale masks, the current blue palette, 1.25 post-mix saturation and painted green indicators. The source contains a hidden convex Flight collision hull; visual meshes remain unchanged. The prefab retains the previous inertia explicitly and places the engine exhaust at Engine.Main. Full chassis-anatomy and breakup migration remain separate from this visual integration.
