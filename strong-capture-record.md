# Stronger canopy projection capture record

Code: 7bb70b5c9e55d07976449ad2d7d89ddbce5d44be.
Tuner: bf760e415b0d2ef267449bcd6e3ae7f7aa2342b7 on evidence/ui-mockups-20261029.

The owner asked for more visible effects. The saved base profile now uses glow 1.1, aberration 4.5, curvature 0.4, beam jitter 0.85, afterglow 0.24, scanlines 0.5, warmth 0.5, wear 0.32 and glass distortion 0.35. Event settings are unchanged. strong-settings.json contains all twelve values.

## Capture method

Unity 6000.1.8f1, DX11, Performant quality, 1280x720, Preview Host Playtest Combat. The scene is frozen after launch. Locale 3 and Locale 1 show the same combat state. Screen captures use the composited Game View. The capture helper switches URP to compatibility mode, so the scratch capture restores RenderGraph before taking these frames. Film grain is disabled in runtime profile copies for a stable pixel comparison. Capture setup changes are not saved to game assets.

canopy-strong-*-before.png uses a runtime clone with the original combat preset. canopy-strong-*-on.png uses the new saved profile. Both use the corrected floating-point history. The off images disable decorative terms through the component's diagnostic switch. The scene images use an empty HUD through the same render format and composite path.

The tuner uses the same stronger values in custom mode, steady state, with no hit, appearance or transition event. Its raw canvas is captured at its authored resolution. Unity scales pixel-valued settings by output width / 1600. Tuner controls are captured separately.

## Measured results

All six comparisons against the matching empty-HUD reference have zero darker RGB channels across 921600 pixels each. This covers the stronger preset, the original preset and personality off on each sky. See strong-pixel-check.json and check-strong-pixels.py.

The real-camera disappearance assertion reproduced the review finding before the fix. After three seconds, the RGBA8 history retained a 0.00173091888 linear red difference, above the 0.0005 limit. The fix uses RGBA16F history so faint light continues to decay below visible precision. The test passes after the change, and all five projection tests pass. The failing and passing summaries are included. Two full-resolution history buffers now use twice the memory of RGBA8, an extra eight bytes per screen pixel in total. The HUD target remains RGBA8 sRGB.

## Limits

The game uses the current instrument artwork and fonts. These captures compare the effect, not identical layouts. Text over a backing can still contribute to bloom under the accepted alpha-mask design. Event-driven hit, low-hull and transition behavior belongs to slice 3. Stills do not show the full motion of jitter or afterglow.
