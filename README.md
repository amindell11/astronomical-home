# Needle fleck flight check

The owner selected needle fleck in pale gold on 2026-11-08 and requested an in-game trial on 2026-11-09. This checks the replacement implemented in commit `6d7464e42cf3e4a182c5e439f8253a426fccbfb7`.

The actual Rippers weapon fires the authored projectile through the game's presented GameHost and ObserverCam, against IllustratedBlue. The visible streak is 1.1 world units long. The current game's 60-unit-per-second speed is preserved. The pale palette and geometry follow [the approved source](https://github.com/amindell11/astronomical-home/blob/6e6b69feef17175c38b3225f3748e6c857ae8b10/history/14-ripper-needle-fleck/selected-source.json).

The capture fired 14 shots and found bright warm pixels at the projected projectile positions. The source contains 101 rendered frames, with simulation time advancing by 0.02 seconds per frame. The MP4 plays at 50 frames per second. The GIF uses every second frame at 25 frames per second, so both show simulation-speed motion. Slow image acquisition made the wall-clock timestamps unsuitable for judging weapon speed.

The five existing LaserHitVfxPlayModeTests passed, including flight fade and pooled reuse. The capture scenario passed. Source baking preserved both impact textures byte-for-byte. The diff contains no C#; the ReSharper ratchet skipped it and the Size-S quality pass checked the six changed files directly.

This is a scripted firing scene through the game camera. The separate playable build uses the normal InitScene and hangar for the owner's flight.
