# Ripper bloom and gold color

The owner requested stronger bullet bloom, then rejected the first uniform brightness increase because the bullets looked white.

The final capture runs commit `dd6c2b2bdf32f84f6ea8c97cbd6af164b37422cd`. Ripper material RGB is 1.5, 1.15, 0.45 with alpha 1. The importer writes the same tint. The six-frame texture, dimensions, faint trail alpha, projectile speed and firing cadence are unchanged.

The game-camera scenario passed, including live material-property assertions for the warm tint, single projectiles per event, and visible pixels near their projected positions. It fired 28 bullets and observed all six static variants. The selected proof frame contains 70 brightened pixels and 8 warm pixels. The rejected white version's selected frame had 70 brightened pixels and no warm pixels. These frames are not a controlled pixel-by-pixel comparison.

Both captures use actual GameHost, ObserverCam, Rippers and pooled projectiles against IllustratedBlue at High Fidelity. They are scripted firing scenes, not a full combat playthrough. The GIFs keep alternate frames at 25 FPS, lasting 2.04 seconds. MP4s retain 101 frames at 50 FPS.

Compile checks passed for all nine assemblies. The follow-up quality review found no issues in the two-line material and importer change. The 145-test weapon and hangar run belongs to the unchanged firing implementation at `cd2baa18e`. The pool merge runs the complete suite on its landing tree separately.

`ripper-rejected-white.*` records the rejected version at `922cf2b2f9c3970c618fa3fe93b585492f9b82eb`. `ripper-gold-bloom.*` is the corrected version. Capture source, telemetry, producer provenance and test summaries accompany both.
