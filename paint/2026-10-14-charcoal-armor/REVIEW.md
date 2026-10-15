# Charcoal front and side armor

The owner selected recoloring the whole armor plates to charcoal rather than adding another brush color. `09 Side armor.002` and `15 Front armor` now share `Bulwark Armor Charcoal`, using the internals' base color `#302E34`. The material's Base Color remains editable independently of the internals. Existing stripes are no longer displayed on those plates.

The change was applied directly in the live scene while the owner edited the front plate. Stored mesh fingerprints and an additional Edit Mode vertex/face/UV comparison confirm that this operation changed no geometry or UVs. Every other part's fingerprints match the immediate live baseline.

The independent armor paint buffer was preserved exactly in `ArmorBaseRegion.png` and a packed `Bulwark Armor BaseRegion` image. This also separates its external path from the hull's BaseRegion file. All existing paint pixels were retained.

The agent did not save over the owner's ongoing Edit Mode session. The owner should save the Blender file with their current edits; these results describe the live application, not a verified saved-file reload. `live-applied.json` records the checks and `apply_live_charcoal.py` records the operation.
