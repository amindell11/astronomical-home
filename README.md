# Laser impact shape and motion studies

First procedural sketches for [Direct-hit effects for lasers, rippers, and railguns](https://github.com/amindell11/astronomical-home/issues/792). This evidence branch never merges. The user selected B, Directional fan, on 2026-10-21. The [drawn refinement comparison](history/02-refinement/README.md) now awaits a source choice. No final sprite is approved yet.

## What to compare

- A, Compact snap: a short contact pop with fast sparks, ending at 240 ms.
- B, Directional fan: broad tapered energy shapes kick away from contact, ending at 340 ms.
- C, Split flare: two torn lobes and a longer blue flicker, ending at 460 ms.

Those times describe the energy sparks. Asteroid chips/dust can linger for 520 ms in this sketch. Ship contact gets small pale sparks; asteroid contact gets heavier gray/tan chips and faint dust. All three share the shipped laser's flat blue/cyan/mint/white bands. The projectile travels from right to left.

These are motion and shape studies on the existing game style reference, not Unity captures, not final asset art, and not a proven game-scale integration. The scale-proxy panels preserve the source reference's pixel scale; the lower row enlarges only the energy shape by 2.15 times. No shield, hull-damage state, muzzle, missile, or concussion effect is introduced.

## Files

- `index.html`: interactive comparison with normal/quarter-speed playback, scrubbing, and a rapid-hit toggle. Serve this folder with a local static server, or open it directly in a browser.
- `comparison.png`: all three shapes at 55 ms after contact.
- `follow-through.png`: later shape and target debris at 160 ms.
- `motion-quarter-speed.gif`: one contact at quarter speed, with a short empty tail between loops.
- `motion-quarter-speed.mp4`: the same 55-frame study as an H.264 clip for playback and download.
- `rapid-hits.png`: three-hit sketch with contacts spaced 140 ms apart, viewed at 220 ms. This interval is an authoring probe, not a statement of any weapon's actual cadence.
- `style-frame.webp`: an unchanged copy of `art/vfx/laser-bolt/refs/style_frame.webp` from the project.
- `capture.cjs`: headless Playwright captures and interaction/layout checks. Requires `playwright`; optionally set `CHROMIUM_EXECUTABLE` to an installed Chromium binary.
- `animate.py`: assembles browser captures using Pillow. Run after `node capture.cjs`.

To regenerate the MP4 with a full FFmpeg build, run:

```sh
ffmpeg -y -framerate 12.5 -i frames/%03d.png -vf 'pad=ceil(iw/2)*2:ceil(ih/2)*2' -c:v libx264 -pix_fmt yuv420p -movflags +faststart motion-quarter-speed.mp4
```

The palette follows `art/vfx/laser-bolt/crackle/crackle.py`. The rough shapes are deterministic canvas paths, following the cheap-sketch step in aesthetic-authoring. There was no image-generation call in this round. Refinement follows the user's shape choice.

## Review

Browser checks: no page errors; time scrub updates the scene; speed and rapid-hit controls respond; the layout has no horizontal overflow at 390 px. Stills were inspected for contact placement and legibility. This does not claim runtime performance, Unity placement, damage behavior, or production effect lifetime verification.
