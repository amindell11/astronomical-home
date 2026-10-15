# Bulwark parts organization

The owner requested clear labels and organization for the parts retained in the ship.

The 21 used meshes are grouped under Hull, Armor plates, Cockpit, Propulsion and canisters, Upper flaps, and Lower flaps. Pivots and mirror planes have descriptive names in a separate helper collection. The two flap deployment controls stay with their flap assemblies. Unused geometry and empty branches are retained under `ignore / Unused parts and helpers`.

All 75 scene objects are preserved. No parenting, transforms, modifier references, mesh geometry, UVs, material assignments, or paint pixels changed. Side armor now belongs to `role.hull`; the metal canisters moved from `role.canopy` to `role.hull`.

## Saved paint

The saved owner checkpoint still had recent BaseRegion brush strokes only in its live image buffer. The reload check caught the stale packed image. The live buffer was backed up, saved to `layers/BaseRegion.png`, packed, and compared again after a fresh Blender reload. Pixel hashes match the owner's live buffer exactly.

## Verification

- `verification.json`: all source and evaluated world geometry, topology, UVs, transforms, parents, modifier references, materials and live pixels match before/after.
- `saved-verification.json`: fresh reload passes for all 75 objects and all captured image pixels.
- `check-before/ship_check.json`: 75 existing findings.
- `check-after/ship_check.json`: 51 remaining findings: origin 21, scale 20, ngons 6, mirror 4. Organization and correct export membership removed the role findings and findings on ignored leftovers. Remaining geometry findings were not changed or exempted.
- The existing geometry lock and prior UV-density proof remain stale after owner edits; this cleanup does not certify those stages.

## Current Blender view

![Organized Outliner](hierarchy.png)

![Current ship](ship.png)

These are Blender editor captures, not in-game validation.
