# Longer mine center

The user approved the missile and requested a longer mine inner explosion.
Implementation `9ab993bd` extends the center from 0.14 to 0.40 seconds. It keeps
the fast initial expansion, holds the bright center briefly, then fades it as
the disc grows. The missile and the mine's 0.8-second sweep are unchanged.

`unity-preview.gif` and `unity-preview.mp4` show two replays through the game
camera. The 179-frame recording plays at 50 fps, with 20 ms GIF frame delays.
`unity-mine-center.png` shows the center still visible about 0.26 seconds after
detonation. This capture covers appearance and repeated playback, not flight or
audio.

The scoped PlayMode run passed all 19 tests, recorded in
`unity-test-summary.json`. `WeaponExplosionPreview.cs` is the capture scenario.
`browser-checks.json` verifies the longer core fade, unchanged missile lifetime,
transparent atlas borders and final frames, and the preview controls.

`preview-260.png` shows the same timing in the drawing preview. Download
`preview.html` to open the standalone tuner. `settings.json` contains the export
settings. The missile and disc texture hashes match the preceding implementation.
