# Approved cockpit canopy captures

Code: 6cef1bebc63c7bcd05dfbc463fa01366504143ed.
Reference tuner: bf760e415b0d2ef267449bcd6e3ae7f7aa2342b7.
Owner source: owner-settings.json.

The eleven retained profile values match the owner's export exactly. On 2026-10-29 the owner approved replacing the separate CRT curvature and sinusoidal glass distortion with one cockpit-shaped canopy field. The obsolete curvature control is removed. The source export keeps the original value for provenance.

## Capture method

Unity 6000.1.8f1, DX11, Performant quality, 1280x720, Preview Host Playtest Combat. The scene is frozen after launch. Locale 3 and Locale 1 show the same combat state. Screen captures use the composited Game View. The capture helper switches URP to compatibility mode, so the scratch capture restores RenderGraph before taking these frames. Film grain is disabled in runtime profile copies for a stable pixel comparison. Capture setup changes are not saved to game assets.

canopy-owner-*-on.png uses the owner's values and approved geometry. The off images disable decorative terms through the component's diagnostic switch. The scene images use an empty HUD through the same render format and composite path. All four comparisons have zero darker RGB channels across 921600 pixels each. See owner-pixel-check.json.

The pinned tuner images use the owner's original twelve-value export, including its obsolete curvature term. They show the source look for the remaining effects. They are not a geometry parity claim, because the owner approved replacing that geometry. The game uses its existing instruments, colors and fonts.

## Distortion map

cockpit-glass-map.html compares the previous geometry with the approved geometry now implemented in the shader. Its right-hand field matches CanopyRadius and CanopyOffset. The quiet center, arched upper rim and flatter lower lip form one warp. The dashed contour is a shape guide, not cockpit artwork or an occlusion mask. Only the HUD samples move; the world remains unchanged.

The map shows actual sampling-offset magnitude at 1600 by 900. It solves the inverse map for the displayed HUD grid, then magnifies the grid displacement four times by default. The button switches to actual displacement. Magnitudes are never exaggerated. The map's reference resolution and the shader's width scaling agree for the captured 16:9 frame.

## Validation

Six local projection tests pass, including an unchanged scene with every effect at maximum, additive light, text blending, real camera rendering and resize, afterglow decay, and the new quiet-center and symmetric-side GPU assertion. The combined quality review found no issues in the final geometry diff. The map has no JavaScript errors, responds to its magnification control and fits a mobile viewport without horizontal overflow.

The afterglow regression was reproduced before the RGBA16F history fix. The two full-resolution history buffers now use twice the memory of RGBA8, an extra eight bytes per screen pixel in total. The HUD input remains RGBA8 sRGB.

## Limits

Text over a backing can contribute to bloom under the accepted alpha mask. Instrument artwork, fonts and color roles are outside this slice. Hit shudder, appearance flicker and transition tears have shader support but await game-event wiring in slice 3. These stills do not establish motion quality or frame-time performance.
