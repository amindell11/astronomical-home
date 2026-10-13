# Bulwark

`Bulwark.blend` keeps the approved editable parts, live modifiers and hinge controls. The cockpit's `PaintUV` chart is continuous over the glazing, with its boundary beneath the surround. `lock.json` protects geometry; verify it before further paint edits.

`layers/Shadow.png`, `Light.png` and `Ink.png` are independent grayscale masks. `BaseRegion.png` selects the plum nose stripe and bone flap tips; colour remains in the material base coats and `palettes.json`. The images are packed in the Blender source. After external mask edits, reload and repack the images before saving. `UV-guide.png` supports hand edits, and `paint-settings.json` records the preview shader bindings and strengths.

The plum palette and UV layout are approved. Paint masks are awaiting review; native Unity material previews live on `evidence/bulwark`. Gameplay prefab integration and flight check remain separate.
