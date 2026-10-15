# Bulwark

`Bulwark.blend` keeps the editable parts, live modifiers and hinge controls. The owner is editing the geometry; `lock.json` records the earlier approved geometry and needs renewed owner approval after those edits. The cockpit's `PaintUV` chart is continuous over the glazing, with its boundary beneath the surround.

`layers/Shadow.png`, `Light.png` and `Ink.png` are grayscale paint masks. They retain broad shading, canopy reflection and deliberate panel lines; automatic edge wear and chips are removed. `BaseRegion.png` controls solid bone-coat opacity over the underlying plum or charcoal. One angular rear stripe crosses the top plates and armor shoulders, then wraps down the side armor. `EdgeCoat` carries the same stripe across charcoal plate edges. The nose coat crosses onto the adjoining armor panel with an angled rear boundary; the nose stripe and fin-root wedges stay plum. The exposed frame, engine housings, aft bridge and plate undersides use the charcoal material. Colour remains in the material nodes and `palettes.json`. Use Material Preview to see the packed masks. After external mask edits, reload and repack the images before saving. `UV-guide.png` supports hand edits, and `paint-settings.json` records the preview shader bindings and strengths.

Texture Paint opens on `Bulwark BaseRegion` with an opaque hard white brush. White adds the existing bone-white coat; black reveals the underlying color. Press **X** to swap, **F** to resize, and **Alt+Q** over another part to transfer Texture Paint to it. Plum, Bone, Flap and EdgeCoat materials accept the white coat. Use the Image menu to save edited images, then save the `.blend` before handing the rough paint back for cleanup.

The owner's asymmetric livery has cleaned panel borders and paired side stripes. The two additional plum nose bars wrap over the nose edge to the underside. Packed masks match the external PNGs, verified after a fresh Blender load.

The side canisters use the neutral gray `Metal` material; `Glass` belongs to the cockpit glazing. Metal has its own finish settings in `paint-settings.json`.

The plum palette and UV layout are approved. The owner's paint cleanup is applied in the editable source; review images live on `evidence/bulwark`. Gameplay prefab integration and flight check remain separate.
