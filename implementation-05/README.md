# Upright mine core and circular ripples

Implementation commit `f495844f1374149986d6ebc5c4f347a4d874657d` removes core
rotation and the radial streaks over the mine disc. The core still pulses, and
broken circular ripples move through the painted disc. Both pointed core tips
and soft edge transparency from the preceding correction remain intact.

The mine center lasts 0.40 seconds and the disc sweeps for 0.80 seconds. The
missile keeps its approved animation and timing.

The Unity clip contains 179 frames at 50 fps, lasting 3.58 seconds. Both effects
replay twice through the game camera. The associated 20-test PlayMode run passed
with no failures or skips. The capture verifies appearance and pooled replay;
it does not exercise missile flight or audio.

The drawing preview uses the same renderer as the exported Unity textures.
The regression measurements check complete tips, edge coverage, retained hue,
disc rim alignment and transparent animation-frame borders. Earlier visual
revisions remain in the preceding implementation folders for comparison.
