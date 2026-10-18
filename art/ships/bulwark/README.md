# Bulwark

`Bulwark.blend` keeps the editable parts, live modifiers and hinge controls. The owner is editing the geometry; `lock.json` records the earlier approved geometry and needs renewed owner approval after those edits. The cockpit's `PaintUV` chart is continuous over the glazing, with its boundary beneath the surround.

`layers/BaseRegion.png` is the approved, symmetric bone-white coat over plum or charcoal. The forward stripe runs parallel to the hood edge and continues across the nose panel joint. The front and side armor and shoulder insets are charcoal; shoulder trim is plum. Geometry, UVs and the base-coat mask are unchanged by the finish pass.

`layers/FinishShadow.png` and `FinishLight.png` supply clean surface shading beneath the deliberate ink lines. Both plum and white receive this finish. These grayscale masks retain recolorable material colors; they contain no procedural wear, chips or grain. The original Shadow, Light and Ink images remain packed, with the canopy retaining its original masks. All image data is preserved for editing. Use Material Preview to see the packed textures. After external edits, reload and repack the images before saving. `paint-settings.json` records the Blender finish and existing Unity bindings; the Unity preview has not yet been updated to this finish.

Texture Paint opens on `Bulwark BaseRegion` with an opaque hard white brush. White adds the existing bone-white coat; black reveals the underlying color. Press **X** to swap, **F** to resize, and **Alt+Q** over another part to transfer Texture Paint to it. Plum, Bone, Flap and EdgeCoat materials accept the white coat. Use the Image menu to save edited images, then save the `.blend` before handing the rough paint back for cleanup.

The side canisters use neutral gray metal with restrained directional highlights. The cockpit uses blue glass with a broad soft reflection. Colors remain in the material nodes and `palettes.json`.

The base coat is owner-approved; the Blender finish is ready for review on `evidence/bulwark`. At the owner's request, the outdated flight/geometry lock and UV-density prerequisite are deferred while finishing Blender textures. Gameplay integration, in-game paint approval and breakup remain pending.
