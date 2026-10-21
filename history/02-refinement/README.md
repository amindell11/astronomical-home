# Directional fan refinements

The user selected B from the procedural study on 2026-10-21. The [selection ruling](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6325470549) keeps the contact core, backward energy fan, and 340 ms timing starting point.

This round compares B1, Broad bands, and B2, Torn bands. Both came from the built-in image generator with the same selected sketch, shipped laser source, and game style frame as references. These are two contour treatments, not an nb2/pro model comparison. The exact prompts are in `prompts.json`; each source's sidecar records its prompt and reference hashes. Generated pixels and transparency are unchanged.

B1 keeps a more continuous bright center at 32 and 48 pixels. B2 retains more jagged breaks and stays closer to the procedural sketch, but some breaks disappear at small size. B1 is the recommendation, pending the user's choice. Both still need the user's source cleanup, particularly flattening residual gradients and stray edge pixels, before generator work.

## Files and reproduction

- `selected-b.png` is the selected procedural shape at 55 ms. `render-selected.cjs` renders it directly from the original study.
- `bolt-flat-reference.webp` is an unchanged copy of the shipped laser's source. The game style frame remains at `../../style-frame.webp`.
- `b1-broad-bands.png` and `b2-torn-bands.png` are unchanged 1254-square RGBA outputs. Each has alpha values from 0 to 255.
- The matching JSON sidecars record the generation provenance. `prompts.json` holds both full prompts together.
- `index.html` displays the originals enlarged and on the existing ship/asteroid reference. Its slider adjusts the visible solid width from 24 to 72 pixels. Open it in a browser.
- `comparison-48px.png` and `comparison-32px.png` capture that page. `capture.cjs` reproduces them with Playwright and checks the width control and narrow-screen layout. Set `CHROMIUM_EXECUTABLE` to an installed Chromium binary if needed.
- `browser-checks.json` records no page errors and no horizontal overflow at 390 pixels.

The scale comparison uses the bounding box of pixels above 240 alpha for equal solid widths and the center of the white core for contact placement. This is display alignment only; neither source is altered. These are static art comparisons, not Unity captures, a revised animation, or proof of final game size. The timing remains the selected procedural study until the cleaned source is ready for animation.
