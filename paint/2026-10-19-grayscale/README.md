# Crimson grayscale migration — 2026-10-19

## What is being reviewed?

Crimson's original palette, geometry, UVs, canopy and runtime lighting, using the
shared grayscale Shadow/Light/Ink pipeline plus authored palette coverage. This is
a review candidate, not approval of an exact visual match. The owner requested
staying grayscale until dynamic hull shadows land; coordination with that work
confirmed the existing texture contract and no shared shader changes.

The top and bank comparisons show the warmer gray shadows, changed rim treatment,
and more visible brushwork on red/charcoal parts. The source atlas includes colored
multiplicative shadows and highlights that the current grayscale composition does
not express exactly with the original palette. Shared recipe cap boundaries also
differ from the historical largest-polygon recipe. See comparison-metrics.json.

## What does the evidence prove?

- Actual Unity prefab renders in Performant quality, 1024px and 256px, with fixed
  transforms, camera, ambient and directional light. Baseline repeats were pixel
  identical for all four poses. Initial frames affected by material readiness were
  replaced by synchronous compilation and two renders per readback.
- Baseline is an in-memory clone using the retained original atlas and original
  material settings. Converted uses the actual imported production material/maps.
- The two central glass samples match exactly. Asset checks prove canopy, FBX,
  chassis prefab, shader code and material GUID files unchanged; this does not claim
  every canopy pixel is isolated by the sample test.
- The temporary cyan palette proof changes three palette colors only. It does not
  propose a new production palette. No texture or material asset is saved by it.
- Lineup, left to right: Valis, Crimson, Vanguard, Nightshade, at prefab scales.
- All 54 existing ship anatomy / DrawnSurface / DrawnCanopy tests passed. These are
  routed local checks, not a full-suite merge result.
- Source geometry and UV fingerprints are unchanged. ship_lock and ship_uv pass;
  ship_check records pre-existing source convention failures that were not repaired.

## How was it produced?

The shared art/tools/ship/ship_paint.py generated every layer and baseline. The
scratch scripts here assign original regions, bind generated layers for Blender
editing, transfer paint properties into existing Unity materials, and capture or
verify the result. They are provenance, not a second paint generator. Current
sources/settings are in the conversion PR; no shared tool changes are required.

Raw before/after captures are under baseline/ and converted/; comparison images
place those same captures side by side. All source/runtime assumptions checked by
verify_assets.py are recorded in final-verification.json.
