# Turbolaser and ripper shape study

The owner chose the compact slug with a short tracer tail on 2026-11-07. The sheet marks it selected. The dart and pellet remain as comparison sketches.

These are native canvas shapes and a browser rendering of the approved laser source. They are concept evidence, not a Unity flight capture. Reference pixel sizes are proposals for comparison, not established world sizes.

- `shape-study.png` compares the approved laser and magenta turbo impact, then the ripper projectile and simpler hit.
- `ripper-slug-sketch.png` is the selected shape on a transparent 1024 by 512 canvas for refinement.
- `ripper-impact-sketch.png` is the simpler hit on a transparent 1024 by 1024 canvas for refinement.
- `impact-timing.gif` and `impact-timing.mp4` show a 1.5 second repeating timing study. `impact-comparison.png` holds all hits 30 ms after contact.
- `study.html` is a self-contained preview. Its button switches between the sheet and timing comparison.
- `study.template.html`, `build.cjs` and `capture.cjs` produce the preview and captures. `checks.json` records the checks.

The turbo uses the approved laser drawing and motion with a runtime hue rotation of 100 degrees and saturation 1.15. The source stays unchanged. Both keep the 340 ms source timeline, hold full opacity through 60 ms, and fade to zero at 280 ms. Ripper uses a proposed 180 ms lifetime with a narrower burst and sparse sparks.

Checks found no browser errors or layout overflow. Laser and turbo have identical alpha footprints at 0, 60 and 140 ms, with zero visible pixels at 280 and 340 ms. Ripper has zero visible pixels at 180 ms. The approved source SHA256 remains `0eb51ba237edc0a04427f49863250b4d234705e9a07e1de12a1da3061c9e6108`.

Rebuild with `node build.cjs <repository-root>`, then `node capture.cjs` with Playwright available through `NODE_PATH`. The capture script names the installed Chromium executable. The timing clip uses the numbered frames at 60 frames per second.

## Selected pass for review

`selected-pass.png` and `selected-pass.html` focus on the compact slug, magenta turbo hit and simpler ripper hit. `selected-pass.gif` shows both in motion. `selected.js` adds that view, and `capture-selected.cjs` captures it. The original three-choice sheet remains evidence.

Two generated refinements were rejected because they added stray cyan texture. The clean native sketches remain the review candidate because the owner asked for less detail. The owner has selected the compact slug silhouette, but has not yet approved this final art treatment or its proposed size and timing.
