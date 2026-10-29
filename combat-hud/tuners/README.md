# Live canopy tuners

Open `personality.html` or `color.html` in a WebGL browser. Both are self-contained and run offline. The pages share the same renderer and controls, with the relevant section opened first.

Start with Explore strong. Every effect starts above zero. Use Personality on to compare against the baseline, or Zero all to remove every effect while retaining individual controls. The moment buttons replay appearance, reload, keystone recovery, damage and low hull. The sequence also plays the phase-start and extraction-open banners. Three further banner samples are below the export.

Copy JSON or Download JSON exports the chosen values. The page does not post a choice to GitHub; the owner returns the block for a dated ruling on #684. Combat, Menu and Big moment presets remain drafts. The console session will replace its two context presets.

## What is in the captures?

The inputs are copied without image edits. Only the HUD enters the shader's distortion and texture passes. Final compositing adds its RGB light to the unchanged scene sample.

| View | Source and limit |
| --- | --- |
| Bright Locale 3 | The saved Unity sky capture `locale3-baseline.png` from the October 4 locale study. It contains no gameplay ships; this view therefore omits ship-attached HUD markers. This is the available bright locale test, not a newly captured combat frame. |
| Copper & Turquoise | `gameplay-final.png` from evidence commit `a1f83ad693375be941ffd6bb62ee46fe887c93c3`, also used for the original canopy concepts. |
| Blue combat | The unchanged `capacity-scene-backdrop.png` from the October 26 capture handoff. It adds pale asteroids and actual world targets to the readability check. No rejected UI-study code or assets were reused. |

Instrument values are sample states for animation. The shapes are review drawings, not final glyph assets. The color page shows each role additively over the selected capture; Console backing has its own opaque sample and never appears behind the combat HUD.

## What carries over to URP?

All demonstrated effects can be expressed in a URP UI material or render pass. The HUD needs its own render texture, a stroke/glyph mask for bloom, and two history textures for afterglow. The final scene composite uses additive blending. Curvature, refraction, RGB separation, scanlines, beam noise and shudder operate on HUD sampling coordinates or HUD light, never scene coordinates. Event ages drive appearance flicker and the short transition tear.

The mockup draws text and vectors with browser canvas. Unity would render the selected fonts and glyphs through its UI path. Shader integration and performance on game hardware are untested; this branch changes no game code or assets.

Continuous beam jitter and conspicuous aberration deliberately push past the earlier quiet-combat preference. The owner can see their range before reducing them. Appearance flicker uses two dips of at most 17 percent per event. Low hull has a steady informational red pulse plus irregular personality shudders; the pulse remains when personality is off.

## How was it checked?

Chrome loaded both pages with no page errors or external requests. All six embedded fonts loaded. Each of the twelve personality knobs changed rendered pixels. The off switch produced identical baseline pixels with strong settings and with zero settings. Pixel comparisons on all three captures found zero darkened channels. Dim opacity and color controls changed their render or export, and downloaded JSON matched the displayed block. The layout had no horizontal overflow at widths 375 and 768.

`verification/` contains the measured results and browser views of strong and baseline rendering. Baseline strokes and digits remain visible on the supplied bright and Copper & Turquoise views. These checks do not establish readability over every possible gameplay background.

## How are the pages rebuilt?

Run `python combat-hud/tuners/build.py` from the evidence checkout. It reads the shared files, fonts, inputs and `source/`, and writes both HTML pages beside the script. Python's standard library is sufficient. A missing input or write error ends the build with a nonzero exit code.
