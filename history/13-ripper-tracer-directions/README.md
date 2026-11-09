# Ripper tracer directions

## Why revisit the projectile?

On 2026-11-08 the owner found the ripper tracer shipped in [PR #1387](https://github.com/amindell11/astronomical-home/pull/1387) too bulky and sluggish. They liked its abstract shape but wanted diverse directions that stay bright and streaky while reading differently from laser bolts. They also asked to see the warm yellow palette tuned.

These are concept sketches for [the hit-effects arc](https://github.com/amindell11/astronomical-home/issues/792). No direction or replacement asset has been approved. The next decision is a shape direction and a color, before further refinement or a game integration.

## What does the comparison hold constant?

Every direction moves at 40 world units per second, the current ripper projectile speed. The reference view uses 20 pixels per world unit. Its scale is a comparison aid, not proof from the Unity camera. The enlarged view exposes silhouette and color choices. The backdrop is a drawn blue-space reference, not a game capture.

The current slug's shape and colors come from `art/vfx/turbo-ripper/draw.js` and `settings.json` as shipped in commit `7c1a44d2ddcc7f16986a08057e5473b3f6035bcf`. Its length is 0.7 world units. The old flight capture remains in [stage 12](../12-turbo-ripper-flight/README.md).

This stage changes no gameplay speed, firing cadence, impact effects, turbolasers, or production assets. The warm-yellow interpretation follows the owner's preceding color request; the exact new shade remains open.

## What is ready to compare?

Six silhouettes range from a single narrow fleck to offset scratches, torn ribbons, trailing embers, an open chevron and a bent shaving. Pale gold is the initial color. Clear yellow, hot amber and the current gold remain available for comparison. Selecting a silhouette updates the four color samples; selecting a color updates all six studies.

The moving lanes repeat projectiles 112 pixels apart to make visual comparison easy. This spacing does not represent the weapon's firing cadence. The GIF plays at normal comparison speed. The interactive study also offers quarter-speed playback.

- `ripper-tracer-directions.html` is the inline comparison source.
- `directions.png` shows all six shapes and the initial color comparison.
- `directions.gif` shows three seconds of motion at 24 frames per second.
- `directions-mobile.png` records the narrow layout.
- `capture.cjs` captures the study and checks its controls and layout.
- `checks.json` records the browser results.

For captures, wrap the fragment as `preview.html` using the visualize plugin's `scripts/render.py`, then run `node capture.cjs --frames` with Playwright available. The wrapper and frame sequence are local intermediates. Encode the frames at 24 frames per second with FFmpeg for the motion preview.

## What was checked?

The enlarged and small views were inspected. Browser checks passed for color changes, shape selection, elapsed-time playback, pause and resume, and reduced-motion startup. There were no runtime errors or horizontal overflow at widths of 736, 510 and 320 pixels. This proves the comparison works, not the appearance or performance of a future Unity integration.
