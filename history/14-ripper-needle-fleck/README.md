# Needle fleck in pale gold

## What did the owner choose?

On 2026-11-08 the owner selected "needle fleck in pale gold" from [stage 13](../13-ripper-tracer-directions/README.md). The [dated choice on the hit-effects issue](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6732123791) records direction A and the pale palette.

This stage isolates that combination without changing its geometry or colors. The small angular tip, thin tapered tail and detached fleck are the selected source. Pale cream supplies the bright core, with warm yellow around it. The native drawing remains editable, so this pass preserves the chosen shape instead of generating another interpretation.

## What remains to prove?

The preview uses the same 40 world units per second and 20 pixels per world unit as the comparison. It is a drawn reference, not Unity footage. Projectile repetition is for visual comparison and does not specify weapon cadence. Camera scale and postprocessing still need an in-game check before judging the replacement in flight.

This selection does not change production assets. The current ripper impact and the turbolaser remain outside this revision.

## Files

- `selected-source.json` preserves the chosen geometry, palette and rendering settings.
- `selected-preview.html` presents the fixed selection with normal and quarter-speed playback.
- `selected-preview.png` and `selected-preview.gif` show the focused preview.
- `capture.cjs` produces the captures and `checks.json` records its checks.

The preview can be wrapped as `preview.html` with the visualize plugin's `scripts/render.py`, then captured with `node capture.cjs --frames` and Playwright. Encode the numbered frames at 24 frames per second with FFmpeg. The wrapper and frames remain local intermediates.

## Review

The still was inspected at enlarged and reference size. Browser checks confirmed the selected shape's coordinates, alpha, glow and colors match stage 13. Playback, pause, resume, quarter speed and reduced-motion handling passed. Earlier yellow widget state does not change the fixed pale-gold choice. Layouts at 320 and 736 pixels had no horizontal overflow.
