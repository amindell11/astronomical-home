# Shared review files

These are mockup inputs for both sessions. No values or type choices are approved.

- `type.css` and `fonts/` provide local fonts. `type-candidates.json` names the two functional families and three heading directions.
- `roles.css` names the current color roles. Dim ink uses the ink hue at lower opacity. The objective starts lavender, apart from player teal.
- `personality.js` exposes `window.CanopyCRT`. The console session owns the `menus` and `bigMoments` presets. Their current values are placeholders.

## How does the renderer connect?

Call `CanopyCRT.create(canvas)`, then `setBackground(image)` with a decoded `HTMLImageElement`. This returns the render width and height. Draw transparent HUD and glow-mask canvases at that size. The glow mask contains strokes and glyphs only, so beam bloom does not soften text. Call `render` once per animation frame:

```js
renderer.render({
  hud, glowMask, settings,
  time, delta, enabled,
  appearAge, transitionAge, hitAge
});
```

Times are seconds. Event ages are `-1` when inactive. `settings` uses the keys and ranges in `CanopyCRT.knobs`; `CanopyCRT.defaults` is the strong exploration preset. `clear()` discards phosphor history, for example when changing scenes. The background never enters the CRT pass. The final pass adds the processed HUD light to the background.

The two tuner pages embed these files at build time. Rebuild them after changing shared values. Exported JSON uses schema version 1 and includes `personalityEnabled`, `personality`, `colors` and `typography`, plus the review context and selected capture. The page labels every export `review-draft` until the owner chooses it.

## Where did the fonts come from?

Fonts and their original licenses are in `fonts/`.

| Candidate | Source |
| --- | --- |
| IBM Plex Sans and Mono | [IBM Plex](https://github.com/IBM/plex), downloaded through [Google Fonts](https://github.com/google/fonts/tree/main/ofl/ibmplexsans) and [Mono](https://github.com/google/fonts/tree/main/ofl/ibmplexmono) |
| Roboto and Roboto Mono | [Roboto](https://github.com/google/fonts/tree/main/ofl/roboto) and [Roboto Mono](https://github.com/google/fonts/tree/main/ofl/robotomono) |
| Michroma | [Google Fonts](https://github.com/google/fonts/tree/main/ofl/michroma) |
| DSEG14 Classic | [DSEG v0.46](https://github.com/keshikan/DSEG/releases/tag/v0.46) |

The interface controls use Plex. The canvas uses the selected family. Headings and mono samples use weight 400; the interface heading uses Plex Sans 600.
