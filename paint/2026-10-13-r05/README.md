# Bulwark

`Bulwark.blend` keeps the approved editable parts, live modifiers and hinge controls. The cockpit's `PaintUV` chart is continuous over the glazing, with its boundary beneath the surround. `lock.json` protects geometry; verify it before further paint edits.

`layers/Shadow.png`, `Light.png` and `Ink.png` are grayscale paint masks. `BaseRegion.png` carries shaded bone-coat opacity over the plum substrate. Paired angled flashes accent the mid-body armor, with smaller marks on the side and rear plates. The nose coat crosses onto the adjoining armor panel with an angled rear boundary; the nose stripe and fin-root wedges stay plum. The exposed frame, engine housings, aft bridge and plate undersides use the charcoal material. Colour remains in the material nodes and `palettes.json`. Use Material Preview to see the packed masks. After external mask edits, reload and repack the images before saving. `UV-guide.png` supports hand edits, and `paint-settings.json` records the preview shader bindings and strengths.

The plum palette and UV layout are approved. Paint masks are awaiting review; native Unity material previews live on `evidence/bulwark`. Gameplay prefab integration and flight check remain separate.
