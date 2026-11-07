# Warm ripper and tighter turbo study

The owner asked for a warmer yellowish ripper and a slightly tighter turbo fan to reflect its longer, more precise bolt. Ripper keeps the selected compact slug and short tracer, with warm amber, yellow and cream replacing cyan. Turbo keeps its impact length and timing while its spread perpendicular to travel shrinks 25 percent.

Turbo's displayed magenta remains provisional until the actual projectile is captured through the game's camera and postprocessing. No production assets changed.

`selected-pass.png`, `selected-pass.html` and `selected-pass.gif` show the proposed pass. The source and capture scripts sit beside them. Run `node build.cjs <repository-root>` followed by `node capture-selected.cjs` with Playwright available through `NODE_PATH`.

## In-game color check

The requested check passed on 2026-11-07 through the actual GameHost, authored camera, HUD and locale, with the real TurboLasers weapon firing through ArmedWeapons. The fixture uses an empty sector and a manual 12-unit camera frame to isolate shots. Postprocessing is unchanged. It recorded 18 shots and found 336 brightened pixels around the strongest recorded live bolt. The in-game image confirms vivid pink/magenta surrounding a pale core, so that palette is retained for the tighter impact.

`turbo-in-game.png` is frame 70, visually inspected before encoding. `turbo-in-game.gif` and `.mp4` play the 101 frames at 50 fps, matching the fixed 0.02 second simulation steps recorded in `turbo-frames.csv`. Wall-clock capture was slower. The initial wall-clock MP4 encode failed its duration read-back, 12.41 versus 12.249302 seconds, and was replaced with the verified fixed-step encode. Both final clips passed read-back. The producer's proof, manifest, summary and scratch scenario are preserved beside them.

The native sheet's pending-color wording records its state before this capture. The owner accepted the compact shape with warm palette and tighter turbo corrections. Combined Unity integration now follows that direction.
