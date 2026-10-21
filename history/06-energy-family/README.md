# Energy hit family

Turbo and charge proposals follow the [approved laser animation](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6328585947). The [prototype scope](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6328639338) records the intended differences and the gameplay facts used in this comparison. Variant settings await visual review.

The approved source, motion generator, and laser settings are read directly from `../05-motion/`. No source image or baseline setting changes. The source hash remains `0eb51ba237edc0a04427f49863250b4d234705e9a07e1de12a1da3061c9e6108`.

Turbo is smaller and shorter to reduce overlap. The six-hit probe uses the prefab's nominal 75 ms cooldown at one fixed contact. It does not simulate physics-step scheduling, aim, travel, heat, multiple weapons, or an actual fight. Charge grows continuously from its minimum legal 30% charge to full charge. The proposed art curve is independent of the damage calculation.

## Reproduction

- `settings.json` holds variant defaults and comparison timing. The baseline remains in stage 05.
- `build.cjs` reads those settings, the stage-05 engine and source, `../../style-frame.webp`, and `tuner.template.html`. It writes `energy-hit-family.html` and prints one JSON object with output path, byte count, source hash, baseline, and settings. A changed source hash or fragment over 1 MB fails nonzero.
- `energy-hit-family.html` is a self-contained in-chat fragment. It starts paused, with single hits and full charge. Playback, charge, turbo burst, scrubbing, and four tuning controls are available. The settings action sends values only when pressed. It is not a hosted website.
- Wrap the fragment using the visualize skill's `scripts/render.py` into local `preview.html`. Then run `node capture.cjs` with Playwright available through `NODE_PATH` and Chromium optionally selected by `CHROMIUM_EXECUTABLE`.
- `capture.cjs` writes `checks.json`, the stills, and 44 frames each for single hits and the turbo burst. Stdout reports check results and the exact frame directories. Each frame advances simulation time by 25 ms, starting at -100 ms. Failed checks exit nonzero.
- `family-25ms.png` compares three weapons, enlarged and at reference-image scale on ship and asteroid contacts. `charge-strengths.png` compares 30%, 65%, and 100% charge. `turbo-overlap.png` shows the repeated-hit probe. `tuner.png` and `tuner-dark.png` show controls. `mobile.png` is ignored local QA output.

Encode `frames/single/%03d.png` at 10 fps for quarter speed, or 40 fps for normal speed. Encode `frames/burst/%03d.png` at 40 fps for normal speed. Use a full FFmpeg build, H.264 with `yuv420p` and `+faststart` for MP4, or `palettegen`/`paletteuse` for GIF. The media files name their playback speed.

## Verification and limits

The source hash and unchanged baseline are checked during build and capture. Every variant has zero alpha at expiry. The charge and burst controls, tuning export, and intercepted settings action are checked. The 320-pixel layout has no horizontal overflow. Screenshots are reviewed for clipping and contact placement.

These are browser studies on a style reference, not Unity captures. Target-specific debris, final game size, actual hit data, runtime pooling and performance, moving targets, and interception/destruction combinations remain unproved. No gameplay, shield presentation, persistent hull damage, projectile art, audio, or Unity asset changes are included.
