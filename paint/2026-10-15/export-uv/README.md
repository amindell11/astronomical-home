# PaintUV in UV0

The Vanguard FBX round-trip failed before the correction: Hull exported MVP_Atlas before PaintUV, and Canopy exported LiveryUV_Work before PaintUV. The drawn shader samples UV0. The exporter now constructs its UV list with PaintUV first, retaining the other named layouts.

This is fix-ladder rung 1, an output construction that cannot put the paint channel after a legacy layout. The fixture keeps a deliberately different, zero-filled LegacyUV. The new smoke assertions failed before the fix and pass afterward.

Validation on the corrected tree:

- Vanguard FBX round-trip passes with PaintUV first on Canopy, Cores, Hull and Ink. Original legacy UV layouts remain present.
- 7 Python contract tests pass.
- Full Blender ship-tool smoke passes (SHIP_SMOKE_PASS), including intact/debris triangle preservation and the new UV-channel assertions.
- git diff --check passes.

The patch records the narrow exporter correction, regression fixture/assertions and one-line output-contract clarification. It changes no source geometry or UV coordinates. Palette/region approval is still pending; no mask generation is included in this proof.
