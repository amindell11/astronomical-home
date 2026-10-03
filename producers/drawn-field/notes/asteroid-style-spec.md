## What is the reusable asteroid style specification?

This supersedes the pipeline emphasis in the preceding workflow proposal. The approved prototype establishes an appearance and layer contract. It does not require existing input meshes, a particular mesh-generation method, fixed counts of ridges/craters/hatch patches, spherical UVs, fixed bake resolution, or the sequence of exploratory scripts. New rocks may be modeled or generated from scratch. Placement and density serve the form and its on-screen size.

## What line style should carry forward?

- Near-black, slightly cool ink. Strong continuous outer silhouettes, selected medium/heavy interior creases and broken crater rims, then finer scuffs and hatching.
- Interior marks have tapered ends and varying pressure. Favor angular changes and chisel-like turns that describe fractures and plane breaks; avoid uniformly outlining every polygon.
- Crater rims are deliberately incomplete and irregular. Hatching uses short directional groups with occasional crossings; it follows the surface and leaves open painted areas between groups.
- Marks should explain shape, wear or a local recess. Avoid isolated arbitrary black blotches, uniform grunge, evenly distributed ticks, and an all-over wireframe appearance.
- Line placement stays attached to the surface as it rotates. Ink is intentional surface drawing, not a substitute for large light-dependent shadows. Drawing should not cast its own ribbon-shaped shadows.
- Tune widths and spacing at the intended game-view size. Preserve the hierarchy, not an absolute mark count or a universal pixel width.

Current prototype calibration (reference values, not asset-generation requirements): contour width parameter 7.5px, lit-side width fraction 0.85, contour RGB (0.003, 0.004, 0.009); surface graphite RGB (0.002, 0.003, 0.005). Interior ribbons use world-space widths, so their final apparent weight depends on asset size and camera framing. The 2.2x widening operation was an iteration, not a reusable style parameter.

## What makes the shadows dark and drawn-looking?

The material uses a broad stylized light/shadow separation instead of ordinary continuously graded diffuse shading. A smooth threshold on the surface-normal/light-direction dot product blends from a cool shadow tint to the lit color. This groups the form into larger readable shadow planes while retaining a controlled transition; it is not a fixed painted shadow or a hard polygon-by-polygon outline.

Real shadow-map attenuation both moves shading toward the shadow tint and applies extra darkening. Palette-bounded lighting multiplies the shadowed result by the controlled illumination, so ambient light does not simply add a bright wash back into those shadows. Specular highlights are disabled for the matte painted finish. Mesh shape and mapped surface normals provide the actual forms that this treatment shades.

Current calibration: ShadowThreshold 0.55, ShadowSoftness 0.18, CastShadowStrength 0.96, PaletteLighting 1, AmbientStrength 0.35, SpecularStrength 0. ShadowColor RGB (0.35, 0.45, 0.8); surface multiplier RGB (1.9, 1.55, 1.12). These are shader inputs, not displayed swatch colors. The cast-shadow multiplier approaches 0.04 under full shadow before the other factors. Texture-derived dark-mark enhancement is disabled (LineStrength 0): the paint must not masquerade as shadow.

Reference implementation: src/Asteroids3D/Assets/Visuals/Shaders/DrawnComparison/DrawnSurface.shader; current calibration lives in DrawnField/Shape01/Asteroid1Paint.mat and DrawnField/FieldGraphite.mat. The outer-contour implementation is DrawnContour.shader, configured by DrawnFieldStudyAssets.cs.

## What are the layer specifications?

| Visual layer | Responsibility | Keep out of this layer |
| --- | --- | --- |
| Form | Asymmetric rocky silhouette, purposeful plane changes, recesses appropriate to the individual rock | Mandatory source mesh or fixed crater count |
| Base color | Medium-value slate/olive-grey mineral color, broad angular brushwork, matte painted variation | Baked illumination, near-black cavity shadows, bright directional highlights |
| Surface relief | Shallow gouges, flakes and scuffs that brighten/darken with moving light; keep broad undamaged planes calm | Global faceting, speckled bump noise, claims that a normal map changes silhouette or cast-shadow geometry |
| Structural ink | Selected creases, broken crater rims and nicks that clarify angular form; dark, tapered, weighted strokes | Full triangulation outlines or large fixed-black regions pretending to be shadows |
| Fine ink | Lighter-weight short hatch bundles, crossings and tiny chisel marks, grouped with negative space | Uniform coverage or equal prominence to structural ink |
| Dynamic shading | Broad cool shadow planes, deep real cast shadows, restrained warm lit planes, matte response | Static painted shadows or glossy highlights |
| Silhouette ink | Bold coherent outside edge, with modest light-side thinning | Interior shell leakage or a generic edge detector that inks every small change |

These are visual responsibilities, not a requirement for seven textures, materials or renderers. Curves, ribbons, masks, painted textures and shader passes remain implementation choices. Current relief strength 0.85 and the packed Blender assets provide a calibration example, not mandatory resolution/topology rules.

## How should future work use this specification?

Use the accepted native Unity asteroid captures as the appearance baseline. Check a new asset at game size, in rotation, and under changing light: form remains readable, ink remains intentional, and large shadows move. Document or expose the few controls that govern the layer hierarchy and shadow response; choose the asset-generation process separately. The generated blue-nebula composite is background art direction only and may reinterpret foreground details; it is not exact native asteroid evidence.
