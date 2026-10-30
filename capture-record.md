# Canopy projection capture record

Code: 83227bdd58ea2982c8bb368608e66f76e55a1ea6, including merged crosshair HUD PR #1194.
Tuner: bf760e415b0d2ef267449bcd6e3ae7f7aa2342b7 on evidence/ui-mockups-20261029.

Both use the combat preset in tuner-settings.json. The game captures use the current instruments and fonts, as required for slice 1. The tuner's shapes, fonts and color roles are reference content for a later redraw.

## Capture method

Unity 6000.1.8f1, DX11, Performant quality, 1280x720, Preview Host Playtest Combat. The scene is frozen after launch. Locale 3 and Locale 1 show the same combat state. Screen captures use the composited Game View. The HUD capture helper switches URP to compatibility mode for gizmos, so the scratch capture restores RenderGraph before taking these frames. Film grain is disabled in runtime profile copies for a stable pixel comparison. No capture setup changes are saved to game assets.

On/off is the component's diagnostic personality switch. The saved profile switch and Preview Host UI belong to slice 2. The tuner is steady at the combat preset, without hit, appearance or transition events. Its raw canvas is captured at its authored resolution; pixel-valued shader settings scale by output width / 1600 in Unity.

## Additive pixel check

For each locale, canopy-*-scene.png uses the same camera, render format and composite path with an empty HUD target. The UI camera culling mask is zero and history is cleared by disabling personality. This isolates the HUD's added light from changes in camera-buffer precision. check-pixels.py compares every RGB channel against this reference.

All four comparisons have zero darker channels across 921600 pixels. The GPU tests separately exercise all effect settings at their maxima and assert unchanged scene pixels for an empty HUD. The screenshots and pixel-check.json are the measured evidence, not a claim of identical instrument artwork.

During diagnosis, disabling the entire projection changed URP from 64-bit intermediate buffers back to its authored packed 32-bit path. That cross-format comparison retained differences up to one 8-bit channel step after the correction. It is not the empty-HUD comparison used above. The original packed implementation had a larger downward bias, especially in blue. The final feature requests 64-bit color only for its registered scene camera, and the shader adds the converted light difference instead of round-tripping zero-light scene values.

## Limits

The alpha mask excludes isolated text from bloom. Text over a backing can contribute to bloom. The owner questioned the need for absolute exclusion; this slice keeps the specified mask instead of adding another HUD render. The owner still judges the visual match. Event-driven hit, low-hull and transition clips belong to slice 3 and are not claimed here.
