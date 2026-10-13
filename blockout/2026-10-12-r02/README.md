# Bulwark blockout — editable groups and cockpit dome

The owner approved seven editing groups: ram nose, upper hull, lower hull, tail booms, tail tips, cockpit, and side insets. The source remains a proportion blockout awaiting owner review.

## Editing

Open `art/ships/bulwark/Bulwark.blend`. Expand `Ship > SymmetryOrigin` in the Outliner. Body and Tail contain the corresponding named groups; Cockpit and Side insets are direct children.

Select a named group to move, rotate, or scale that assembly. Select its numbered mesh and use Tab for vertex editing. Every part retains a live X Mirror using SymmetryOrigin, so the two sides stay linked. The cockpit also has an unapplied Subdivision Surface modifier; edit its simple cage or toggle smoothing to inspect it. Export roles are kept in a separate excluded collection.

## Changes

- Split the existing hull and tail into five closed parts, with internal caps at the separations.
- Retained the original ram/hull/tail exterior and side insets.
- Replaced the shallow cockpit triangle fan with a curved teardrop dome: rounded bulb toward the ram nose (+Y), pointed taper toward the tail (-Y).
- Added meaningful pivots and named parent groups. The file is saved assembled; the exploded image is only a preview.

## Evidence

[Assembled](hero.png) · [Exploded groups](exploded.png) · [Top](top.png) · [Side](side.png) · [Front](front.png) · [Back](back.png)

[Source check](ship_check.json), [orthographic report](ortho-report.json), and [scale report](scale-report.json) passed with source hash `81fed1bd43effbce21f622c59574b414fa41f8c55bc3f93fbcc5da2f6b8c5f37`.

[Editability inspection](editability.json): seven mesh parts, seven live Mirrors, symmetric evaluated pairs, five closed structural parts with outward normals. Cockpit and inset surfaces remain open surfaces at blockout stage. ReSharper skipped automatically because there are no changed C# files.

The 10-unit model length and flat color regions remain placeholders. The raised tail tips follow the approved side view. These are Blender previews; no game integration or detail-model approval is claimed.

