# Nightshade blockout round 1

Source: `art/ships/nightshade/Nightshade.blend` and `ship.json` on
`task/nightshade-hull` at a9750e8a4c3fa48201aad38b3910721ea98b721d.

The owner authorized mesh build after stage-1 approval. O supplies the planform;
R's side supplies thickness and upward tail sweep, and its isometric view supplies
volume relationships. The front is secondary, per the owner's explicit direction.
Concept images disagree in some projections; this first physical interpretation
keeps the compact inward-curving tail pair in top and rising blades in side.

Built and edited in the connected Blender scene, with the sanctioned ship_new
scaffold and approved image planes. Editable mirrored halves remain live.
Broad slate, canopy and magenta materials only; no panel detail, UV work or paint.
The source contains the canopy, fuselage, swept wings, inner fins and curved tails
as separately editable parts in role collections. Packed reference images are
hidden in the ignore collection; toggle it to use the top and side image planes.

## Review

- [Studio top](studio/top.png), [side](studio/side.png), [quarter](studio/quarter.png).
- [Required ortho views](ortho/) and [game-scale strip](scale/scale.png).
- [Eight oblique views](turnaround/).
- [Source check](check/ship_check.json): pass, no findings or exemptions.

Every candidate was shown in chat, including the lit Blender viewport. Proportion
approval is pending; detail work has not begun. Review renders are Blender
previews, not in-engine proof. The studio-side view has its nose left; the
standard tool-side view has its nose right.

The live mesh authoring commands and read-only studio review helper are retained
here as provenance. They are scratch, never tools under art/ships, and must not
be replayed after the owner's first hand edit. This source is then edited in place.
The flat ortho and scale reviews preceded the viewport-only save; geometry is
unchanged. The final check and studio captures name the committed source hash.
