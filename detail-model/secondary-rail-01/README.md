# Nightshade — canopy pane divider and glass fit

Source: [c1fdb274](https://github.com/amindell11/astronomical-home/commit/c1fdb2746421408761f4202dfdef32422c554fdc). **Detail-model candidate; owner review pending.**

The owner identified the inset secondary rail in the approved concept, clarified that it represents the division between glass panes, and asked for the glass to fit the frame cleanly. The divider now sits higher along the glass shoulders, broadens gradually toward the rear collar, and leaves an exposed outer glass band.

![Canopy review](round-03/review-canopy.jpg)

![Before and after](round-03/comparison-canopy.jpg)

<details><summary>Additional review views and validation</summary>

- [Cockpit side](round-03/closeups/cockpit_side.png)
- [Cockpit top](round-03/closeups/cockpit_top.png)
- [Whole ship](round-03/material/iso_front.png)
- [Underside](round-03/material/underside.png)
- [Review sheet](round-03/sheet/sheet.png)
- [Game scale](round-03/scale/scale.png)
- [Source comparison](round-03/compare/compare_top.png)
- [Source check](round-03/check/ship_check.json)
- [Edit audit](round-03/edit-audit.json)
- [Fingerprint delta](round-03/fingerprint-delta.json)

</details>

Round 01 adds the inset rail. Round 02 smooths the upper/lower glazing with live subdivision, adjusts the upper rear shoulder into the collar, and fits the rail to the evaluated glass. Round 03 moves the divider higher on the glass and grows its width from 0.008 at the front to 0.01136 at the rear. Its raised edge also grows slightly toward the rear. The glazing remains one editable surface beneath the pane-divider geometry.

The source control topology, object transforms, owner visibility and live Mirror modifiers are retained. Only the upper glazing's rear control vertices changed; the approved rounded nose and long taper remain. Both glazing pieces gained a live Catmull-Clark level-2 modifier between Mirror and Solidify. The new divider is a separate mesh with live Mirror and Solidify. All other part fingerprints, including the detailed outer frame, hull and fins, match `2a7f4cf5`.

`ship_check` passes with zero unresolved findings and the same nine active owner-approved scale exemptions. The manifest is unchanged. The source is saved in Object Mode. These are Blender previews; no flight, paint or completed detail-stage approval is claimed.

Source SHA-256: `bf15def6d4d91f4c9744262f3920b0f9e0ac58466a9aa362993ef9f8f61cf4d2`. Source and existing manifest are committed on the current task branch. Production legacy hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` are untouched. No PR.

Appended to evidence parent `69034c66765ce72a7da307780a414398a15114a9`, preserving previous rounds.
