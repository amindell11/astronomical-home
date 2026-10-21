# Bulwark reflection balance and slower preview

The owner found the previous 3.24-second GIF too fast to judge and the paired reflections too symmetrical.

Code commit: e07e88616e41de9cdc0689ad47ea48d875229c92.

## What changed?

The shared canopy shader now uses Unity's standard texture tiling and offset for moving reflections. Its fixed Shadow and Ink sampling stays in place. Bulwark selects and broadens one of its existing strokes, with a smaller glint beside it, instead of centering the mirrored pair. A tighter vertical sampling range keeps the thin glint's ends outside the visible glass. No texture pixels, geometry or palette changed in this round.

Other ships keep their identity texture transform. The change adds no runtime animation or ship-specific shader branch.

## How to judge the motion

The preview shows roll from -55 to +55 degrees, then pitch from -35 to +35 degrees. Each pass has 80 rendered frames. The GIF lasts 18.4 seconds and pauses at the extremes. The MP4 uses the same frames and timing and can be paused or scrubbed.

![Slower Bulwark preview](bulwark-slow-preview.gif)

[Pause or scrub the preview](bulwark-slow-preview.mp4)

![Same pace before and after](asymmetry-comparison.gif)

[Pause or scrub the comparison](asymmetry-comparison.mp4)

![Neutral view](asymmetry-neutral.png)

![Banked view](asymmetry-bank.png)

These are controlled Unity asset renders. They do not establish gameplay flight approval.

## Checks

The focused Unity graphics run passes 62/62 checks, including the earlier static-mask and extreme-pitch regressions. Geometry-lock verification passes. The reflections-disabled renders are pixel-identical before and after this round. The whole-ship pixel difference remains inside the cockpit; exact counts and bounds are in asymmetry-proof.json.

The capture code, encoding script, XML report and geometry-lock report are preserved here. The source drawing and recovery scripts remain in the earlier cockpit-glass evidence folders.
