# Vanguard livery-region correction

The owner rejected the whole-part region proposal and asked for the old ship.
The production Vivid top, hero and game-scale captures are the reference under
`paint/retexture/history/production/`. Their region boundaries come from the
original `base_palette.py`, `legacy-paint-layout.json`, and `service-panels.json`.
Wing outlines and core/canopy parameters are recovered from the exact
VanguardStudy source the old recipe read, before its production triangulation.
No placement is guessed from the shaded Vivid atlas.

`old-versus-recovered.png`: old production paint left; recovered flat regions on
the approved detail model right. Top view above, three-quarter view below.
`recovered-regions.png`: cream/orange left, mint/plum right, same boundaries.
`old-and-recovered-scale.png`: old production, recovered cream/orange, and
recovered mint/plum, each rendered at 96 pixels and enlarged four times.
These are Blender placement previews; the new style's masks are not generated.

The corrected proposal restores cream pod faces, orange outer pod borders,
canopy trim, the aft fuselage band with two cream breaks, fin/tip accents, and
blue-gray inner channels. A separate HullInset palette region lets the channels
change color independently of the dark Structure frames and hatch interiors.

The render uses temporary procedural emission materials, without saving a blend
or writing texture masks. Source and baseline hashes are unchanged and the
geometry lock still verifies. See region-correction.json and lock-check/.
The canopy curve is simplified to a maximum 0.0003 source-unit deviation only in
the temporary review shader; no source curve or mesh is changed.

The existing material-slot paint process assigns one color per face. The original
livery bands cross some locked faces. The bounded implementation is to split
only those faces along approved color boundaries, preserve their surface and
interpolate the existing UVs, then refresh the geometry lock and recheck UVs.
That needs owner approval to reopen the lock. A color-neutral livery mask could
preserve topology but would extend the shared shader and paint-tool contract.
Neither encoding has been applied by this checkpoint.
