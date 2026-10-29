# Live hangar console

The owner rejected this rebuild on 2026-10-29. The [restored image-based console](../restored-console/index.html) is current, and `index.html` redirects there. The source and notes below are historical; the owner asked to stop after the small ship-transparency adjustment.

## What does this revision bring across from combat?

The [owner clarified](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6510388690) that the console should adopt the combat HUD prototype's fonts and beam effects. [Open the live page](index.html) to compare them inside the selected drawn hardware.

The terminal letters, category tabs, rules, Launch display and ship contours now render in code through the combat session's `CanopyCRT` pass. The font menus come from its `type-candidates.json`. The build reads the existing `type.css`, font files, `roles.css` and `personality.js` directly. There is no second effects implementation or separate font collection.

The page starts with the same strong exploration preset as the combat prototype, plus Michroma and Plex as its initial preview candidates. These are starting settings, not owner selections. The shared Menu preset remains a draft. Personality off shows the baseline. Replay startup demonstrates the shared appearance flicker and short transition disturbance. Replay selection lights the existing Ship tab. Reduced-motion preference starts the page paused.

The ship has an adjustable faint surface layer beneath a schematic contour drawing. Cool pearl and near-white are comparisons. The nebula shows through the model by construction because these layers add light over the scene. Its silhouette follows the selected concept, but the line drawing is a review approximation, not a production mesh or ship asset.

## How do the shared tuners connect?

The page links to the existing combat personality and color tuners. Paste their schema version 1 JSON export to apply the same effects, colors and type here. The console can export those settings with its own surface and tint values. Invalid imports leave the prior state intact. Fine effect controls remain in the shared tuner.

## What is the background?

`inputs/clean-stage-pro.jpg` is an image-model cleanup of the selected console concept. The model removed the baked menu and hologram so the live layers could replace them. The stage retains the chosen housing and composition, with resynthesized pixels. It is a concept illustration, not an unchanged gameplay capture. The nb2 cleanup changed the hardware and remains saved as an unused comparison.

Both cleanups used the repo image tool, in parallel, with the selected image and named continuity references. Prompt and sidecars are in `inputs/`. The actual Unity HUD screenshots and browser CRT references used in the preceding revision remain in `../hologram-02/inputs/`.

## How was it checked?

Chrome loaded the self-contained page with six fonts, no page errors and no external requests. The heading, effects toggle, surface strength, tint and startup changed rendered pixels. Shared settings round-tripped, and an invalid import kept the previous state. The layout fit widths 375 and 768. Strong and baseline captures are in `verification/`; the baseline text and ship outline were inspected visually.

The renderer adds light only to the drawn layers. The hardware stays outside the effects pass. The glow mask contains outlines and rules, excluding text and broad fills. Unity shader integration and performance remain untested. No game code or assets changed.

## How is it rebuilt?

Run `python consoles/live-hangar/build.py` from the evidence checkout. It embeds the shared producer files, licenses and cleaned stage into `index.html`, which opens offline. `console.js` draws the console review and connects controls to the shared renderer. The optional `verify.cjs` requires Playwright and Chrome and writes screenshots plus `verification/checks.json`.
