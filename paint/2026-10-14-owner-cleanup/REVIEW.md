# Bulwark owner livery cleanup preview

Prepared from the owner's saved rough paint, checkpoint fbee1779. The source geometry and UVs were unchanged during preparation. This preview has NOT been applied to the live source: the apply preflight detected that the owner continued painting and entered Edit Mode. Await the owner's saved handoff, then include their newer strokes before applying.

Preserve the asymmetric layout. Straighten the paired side stripes and white panel borders, remove brush wobble and isolated marks, and retain the broad shading. Per the owner's clarification, the two extra plum nose bars wrap over the nose edge toward the underside, rather than extending toward the tail. The preview projects their fitted boundaries through the nose side faces.

The earlier edge-wear cleanup reached external PNGs but its packed image payloads remained older. The prepared application embeds explicit PNG bytes and verifies them. After applying, validate a fresh Blender load against the packed and pixel hashes before declaring the result saved.

`clean-side.png`, `clean-top.png`, `clean-quarter.png`, and `clean-front-quarter.png` are Blender previews of the prepared masks, using the same forest studio lighting as the owner's viewport. `clean-BaseRegion.png` is a draft mask, not the current source texture. Native Unity integration remains separate.

Confirmed newer live paint delta: 1,157 pixels compared with the captured owner mask. The UV comparison during Edit Mode was inconclusive because the raw UV array is empty in that mode; do not treat it as evidence of a UV edit. Re-export from the owner's final saved state.
