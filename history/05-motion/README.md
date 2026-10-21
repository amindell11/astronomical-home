# Approved laser impact motion and tuner

The user [approved the source as-is](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6327345037) on 2026-10-21. `source.png` is byte-identical to the B1 variant without left spikes, SHA-256 `0eb51ba237edc0a04427f49863250b4d234705e9a07e1de12a1da3061c9e6108`. Do not regenerate it. The earlier hand-cleanup wait is resolved by this approval.

## What is being tuned?

The contact cap stays fixed. The fan opens slightly and its edges move, the white and mint bands fade first, and short outward sparks finish the impact. The duration is 340 ms, with a 48-pixel solid width, 12% fan motion, 20-pixel spark reach, and 25% localized glow. The user [approved this animation and these settings](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6328585947) after reviewing the quarter-speed GIF and MP4 on 2026-10-21.

The source parser identifies four color regions and five outward lobe directions. It retains the original pixel colors, including the approved residual shading. `parsed-bands.png` shows the regions. Time zero draws the original image directly; later frames warp the source and fade each band. The source PNG remains untouched.

## Files and reproduction

- `settings.json` is the shared default settings block. The tuner's export uses the same field names.
- `motion.js` is the motion generator used by the tuner and the captures. Named constants control the remaining timing and sampling choices.
- `tuner.template.html` is the inline comparison and controls. It includes normal/quarter-speed playback, scrubbing, live settings, and a user-triggered settings message.
- Run `node build.cjs` to create `laser-hit-tuner.html`. The fragment embeds its source, style reference, motion code, and settings and remains below 1 MB. The build rejects a changed source hash. Stdout is one JSON object containing the output path, byte size, source hash, and settings; failures exit nonzero.
- `laser-hit-tuner.html` is the in-chat tuner. It is an HTML fragment, not a hosted website. Its state and settings action use the conversation host when available.
- For browser inspection, wrap the fragment with the visualize skill's `scripts/render.py` into `preview.html`. Set `CHROMIUM_EXECUTABLE` if Playwright's bundled Chromium is unavailable, then run `node capture.cjs`.
- `capture.cjs` writes screenshots, 42 animation captures, `parsed-bands.png`, and `checks.json`. It prints one JSON result and exits nonzero for failed checks.
- `motion-quarter-speed.gif` and `.mp4` show simulation steps of 20 ms at 12.5 frames per second, which is quarter speed. They compare the enlarged impact and approximate ship/asteroid size.
- `tuner.png`, `tuner-dark.png`, and `motion-0ms.png`, `motion-100ms.png`, `motion-200ms.png`, `motion-340ms.png` are visual evidence. The mobile screenshot is local QA output.

To encode the captured motion with a full FFmpeg build:

```sh
ffmpeg -y -framerate 12.5 -i frames/%03d.png -filter_complex '[0:v]split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=none' -loop 0 motion-quarter-speed.gif
ffmpeg -y -framerate 12.5 -i frames/%03d.png -vf 'pad=ceil(iw/2)*2:ceil(ih/2)*2' -c:v libx264 -pix_fmt yuv420p -movflags +faststart motion-quarter-speed.mp4
```

## Verification and limits

Frame zero matches a direct render of the approved source under the same browser canvas settings. An earlier comparison mixed CPU and GPU canvas readback paths and found one-channel-unit rounding differences; it did not indicate a source change. Source fidelity is checked with matching render paths and the immutable PNG hash.

The effect has no alpha at expiry. Duration, export, and the settings action are checked; the settings action is intercepted during testing and sends no message. The 320-pixel layout fits without horizontal overflow. The source regions and preview stills were visually inspected. These checks do not prove Unity performance, collider placement, final game scale, or damage behavior. No Unity assets or gameplay code change in this prototype.
