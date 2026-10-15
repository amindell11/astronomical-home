# Bulwark front armor center cleanup

The owner's copied side plates overlapped through the center of the front armor. The final correction retains their original overhang, tilt, outer profile, materials and live chamfer. The source half is cut at the symmetry plane, and the existing Mirror modifier joins that boundary into one closed plate.

The first fitted version changed the silhouette too much and was rejected. It is not the delivered geometry. The owner specifically requested preserving the overhang so the front armor matches the side plates.

## Result

![Front armor](after-front-detail.png)

![Side profile and overhang](after-side.png)

![Whole ship](after-front-quarter.png)

## Verification

- Owner checkpoint: `407cce001156c5040d094fb5fae3e80afff1fcf0`.
- Only `09 Side armor`, renamed `15 Front armor`, changed geometry and UVs. Every other part's geometry, UV and material fingerprints match the owner checkpoint.
- Original outer vertices remain in place within floating-point tolerance; see `profile-verification.json`.
- The evaluated plate is one connected, closed mesh. Mirror and bevel remain editable. See `plate-correction.json`.
- All image pixels match the checkpoint, including the owner's latest paint packed before geometry editing.
- A fresh Blender load reproduces the saved geometry, UVs, materials and paint exactly; see `verified.json`.
- `ship_check` has the same 69 inherited findings, including the plate's existing unapplied scale. No new findings were introduced. Full source validation remains incomplete.
- Native comparison and review sheet renders pass. The earlier geometry lock predates owner edits and does not certify this source; flight approval, current full UV verification, game integration and breakup remain outstanding.

The owner resumed moving the plate in the live editor after the corrected save. Those live adjustments were left untouched. These images and checks describe the saved center cleanup, not subsequent live edits.
