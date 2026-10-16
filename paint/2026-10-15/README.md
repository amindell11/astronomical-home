# Vanguard palette and regions — 2026-10-15

Palette/region review for #1016. The owner approved the detail model and UV layout, waived the flight check, and approved the vent-clearance correction recorded below. Palette approval is pending; no paint masks have been generated.

## Palette concept

![Palette concept](concept/paint-concept.png)

Cream/orange on the left is the proposed default; mint/plum on the right demonstrates an alternate using the same regions. This image is a paint concept, not runtime paint. Imagegen used the exact region render as the geometry/color reference and Crimson's approved concept only for style. The canonical painter supplies the final drawn treatment.

![Exact material-region render](concept/palettes-flat.png)

![Game-scale material regions](concept/palettes-scale.png)

Six named regions carry primary hull, accents, structure, canopy glass, core color and linework. `source/palettes.json` uses linear-sRGB; `concept/region-assignment.json` records the part assignment and display hex colors. All authored detail remains separate from these region colors.

## Approved fit correction

![Before left, corrected right; pod above, cockpit below](fit-review/fit-comparison.png)

Four-corner floors crossed nonplanar hull surfaces. Only the new pod/cockpit vent interiors were raised; original hull geometry and stored UV coordinates remain unchanged. This is fix-ladder rung 2, checked at authoring: the saved-source depth repro failed before the edit and passes afterward. A full refit to hull triangles was rejected as unnecessary topology and UV work.

The corrected saved-source audit samples 1,521 positions on each of the three assemblies and finds zero below-hull samples. `ship_check` and the refreshed geometry lock pass without findings. The existing UV layout passes at density ratio 1.0179469304299966 (limit 2), with no exemptions. Reports and the narrow correction are in `fit-review/`, `audit/`, and `producers/`.

## Sources and next step

`source/` holds the corrected source and lock, ship.json, and the proposed palettes. Scratch authoring producers are archived for provenance, not installed as shared tools. The old atlas and UVs remain recoverable. The owner approval now needed is for the color-region placement and palette(s); masks follow that approval through the canonical ship_paint tool. Final paint is judged in the Unity lineup.
