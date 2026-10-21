# Corrected static mask and reflection cutoffs

The owner identified two defects in the first PR version, 46f38c4a. Reflection strokes also reduced the original Shadow mask, leaving bright fixed stripes underneath the moving ones. The painted-glass shader path did not apply the existing stripe sampling range, so the finite artwork and canopy-shaped extraction exposed end caps during turns.

## What changed?

Commit 7e2e5ffc removes the stroke-shaped cutouts from the packed canopy texture's fixed Shadow channel, using the surrounding glass shade on each scanline. Only those 79,476 red-channel pixels change. The Light, Ink and coverage channels are byte-identical, including the authored stroke shapes. The dark tip and paint outside the strokes stay fixed.

The existing vertical sampling range now applies to painted-glass reflections as well as color drawings. Bulwark selects an interior range, so its original strokes continue past the glass edge. The shared shader keeps the original movement and lighting calculation.

## What caught the defects?

The first PR version fails three new graphics assertions: reflections-off contrast is 0.14377 against a maximum of 0.01; reflection coverage at both extreme-pitch cases is zero against a required minimum of 0.03. The corrected run passes 62 of 62 checks. Both XML reports are attached. The first version's generic fixed-paint check required variation and therefore accepted the ghost stripes; the replacement checks their actual absence.

## What do the captures prove?

These are matched Unity asset renders. The sweep reaches 75-degree roll and 65-degree pitch. It is not gameplay flight approval. The whole-ship comparison changes 2,326 pixels, all inside the cockpit rectangle [291, 120, 341, 259].

![Matched extreme motion](review-motion.gif)

Reflections are disabled in both halves below. The corrected glass has no painted stripes.

![Reflections disabled](review-off-comparison.png)

![Extreme pitch](review-pitch-comparison.png)

![Whole ship](review-comparison.png)

The independent review inspected both pitch directions, the banked view, and the reflections-off comparison. The generator's separate inability to preserve authored canopy materials was reproduced and deferred to #1089.
