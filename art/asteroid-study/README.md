# Drawn asteroid study

`AsteroidReliefStudy.blend` is the current editable source, including the hidden high-resolution scuff sculpt and packed normal bake. `AsteroidDrawnStudy.blend` preserves the drawing-only source. Its rock object packs `AsteroidBrushAlbedo.png`; `AsteroidSurfaceDrawing` is a separate mesh of authored tapered ribbons. Both export under `Assets/Visuals/Environment/Asteroids/DrawnStudy`. The drawing follows selected recess rims and plane breaks, with six small impact bowls, broken rim strokes and paired chisel nicks. It receives lighting but does not cast shadows. `DrawnAsteroidPaintScenario` includes it in gameplay and inspection.

The surface albedo stays medium-value and shadow-free. Broad directional gouache strokes and angular mineral patches supply the painted character. Thin graphite marks are intentional material details; the large dark regions still come from real-time shadows. The asteroid silhouette uses a 5.5-pixel contour. Tapered crease continuations add density along selected ridges while keeping the broad planes open. Production spawning and collision are unchanged.

## Sculpted relief source

`AsteroidScuffNormal.png` is a 1024-square tangent-space normal bake from the Blender sculpt, imported as a normal map. Ten explicitly placed scrape fans and six shallow flaked patches sit beside existing creases and crater rims. The high-resolution source has 176,802 vertices; it is hidden in the saved scene. Its normal field preserves the low mesh's smooth shading outside the sculpted damage. The runtime mesh, albedo, drawing layer and silhouette are unchanged by this relief pass.

The asteroid material opts into normal mapping at strength 0.85. Forward lighting and depth-normal output use the same mapped normal. Other study materials retain their existing shading. Relief affects surface lighting; it does not change silhouette geometry or produce additional cast shadows.

## Current brush texture provenance and prompt

Generated with the built-in ImageGen tool by editing `AsteroidStoneAlbedo.png`. Saved separately as `AsteroidBrushAlbedo.png`. The drawing mesh is authored in Blender and fitted to the rock's UV surface; it is not generated black texture noise.

Use case: precise-object-edit. Edit target: attached clean flat stone albedo texture for a real-time 3D asteroid. Make the SURFACE PAINT distinctly hand drawn: broad deliberate angular gouache brush patches, restrained pencil scumbling and sparse short parallel dry-brush strokes that describe each mineral plane. Preserve medium-value slate grey and muted olive grey palette, but break the existing soft camouflage blobs into more purposeful chisel-shaped interlocking mineral planes. Keep texture scale broad and readable at game size. This is only the base-color layer: a separate hand-authored 3D drawing layer will supply creases, crater rims and nicks. Therefore NO black patches, NO cracks, NO outlines, NO crater motifs, NO shadows, NO illumination, NO highlights, NO orange. No noisy grunge, dense hatching, speckles or mosaic of tiny triangles. All base colors must remain medium value so real-time lighting can shade them. Flat square texture filling the entire canvas, tileable edges, no rendered rock, no border, no text. Retain the original square dimensions.

## Earlier fractured asteroid source

`AsteroidFractureStudy.blend` preserves the earlier mesh and clean albedo before the drawing pass. `FracturedRockReference.png` is the user-supplied style reference.

The Blender mesh has an asymmetric taper, nine carved depressions and raised fracture lips. Its UVs travel with the deformation. The slate/olive albedo supplies medium-value mineral color only. Mesh recesses cast shadows that respond to the light; the surface shader gives light-facing planes their amber palette. The material opts into cast-shadow darkness and disables texture-edge ink. Neither black shadows nor amber lighting are painted into the current texture. The remaining spherical UV compression near the poles needs more deliberate painting before production use.

## Earlier clean texture provenance and prompt

Generated with the built-in ImageGen tool by editing the earlier `AsteroidFracturePaint.png` into `AsteroidStoneAlbedo.png`. This clean albedo is the input to the current brush texture edit. The earlier painted-black candidate remains as exploration history.

Use case: precise-object-edit. Edit target: attached asteroid diffuse texture. Correct it to a genuinely unlit base-color/albedo map for a real-time 3D mesh. Remove EVERY black fissure, black pocket, cavity shadow, directional shading, relief highlight and dark outline. Fill those areas seamlessly with the surrounding stone color. Retain the broad irregular slate-grey and muted olive-grey mineral color regions and subtle painterly brush variation, but keep all colors in a narrow MEDIUM value range: no near-black, no dark cracks, no white highlights. The result must look like flat mottled painted stone color, without any impression of crevices or lighting. Physical cavities and cast shadows will be produced by the mesh and renderer, not this image. No line art, no ambient occlusion, no new cracks, no directional light, no orange, no specular sheen, no 3D object, no text, no border. Fill the entire square canvas and keep edges tileable. Preserve the original square resolution.

## Earlier painted-black candidate (superseded)

Generated with the built-in ImageGen tool using `FracturedRockReference.png` as the style input. Saved as `AsteroidFracturePaint.png`; the earlier mauve texture remains separate.

Use case: stylized-concept. Asset type: production game flat diffuse texture for a rotating 3D asteroid. Attached image is STYLE REFERENCE ONLY. Create a square seamless tileable texture filling the canvas; NOT a picture of a rock. Match the asteroid's hand-painted comic mineral surface: broad irregular desaturated slate blue-grey and olive grey stone planes, with large angular near-black ink fissures, tapered dark clefts and a handful of black scooped cavity marks. Strong value contrast, confident variable-width black brushwork, asymmetrical elongated chisel-like marks. Broad solid colored planes with restrained hand-painted variation. Around 6-8 large connected mineral regions across the sheet, dark creases occupying roughly 12 percent. Irregular branches, selected boundaries only, no uniform outlined cell mosaic. Keep overall colors medium grey to allow engine lighting. Ignore the orange rim in the reference because that will come from real-time lighting. No directional lighting, no orange, no shadows cast across other surfaces, no white edge highlights, no pebble texture, no speckle, no grunge, no dense spiderweb, no regular triangles, no UI, no background, no text. Flat texture, all edges seamless, 1024x1024.
# Earlier quiet paint study

Exploratory source for PR #691, using the asteroid forms in the selected hangar reference. `AsteroidPaintStudy.blend` contains the editable chipped-plane mesh and a packed paint image. The exported FBX and Unity material/texture live under `Assets/Visuals/Environment/Asteroids/DrawnStudy`. This earlier candidate is retained as an exploration source.

The texture was generated with the built-in ImageGen tool, using `doc/design/assets/Pasted image 20260923023842.png` as a style reference. The mesh was built and edited in Blender, with broad irregular faces, bevels and two larger fracture recesses. UVs wrap the painting around the mesh; no screen-space internal edge detector is used for this candidate.

## Texture prompt

Use case: stylized-concept. Generate a production game texture, square 1024x1024, seamless tileable flat diffuse ALBEDO for a hand-painted asteroid. Reference image is STYLE ONLY: study the large rocks at the right, ignore spacecraft/UI/background. The output must be a flat texture filling every pixel, NOT a picture of a rock. Broad irregular quiet mineral color patches in muted warm mauve, dusty rose-grey, desaturated slate violet. Large connected regions, roughly 8-12 broad patches across the whole sheet with softly painterly but decisive irregular angular boundaries. A FEW selected deep charcoal fissures with tapered ink line weight, each a continuous angular branching crack, occupying less than 4 percent of the area. Many color boundaries must remain unoutlined. Restrained graphic comic/game paint, mostly clean and calm surfaces with subtle brush variation. No pebble grain, no speckle, no dense cracks or spiderweb, no triangular low-poly mosaic, no white edge highlights, no cast shadows, no directional baked lighting, no 3D sphere, no text, no borders. Opposite edges should tile continuously. This will wrap around a rotating 3D rock whose lighting is applied in-engine.
