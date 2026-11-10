# Alternating Rippers and synchronized turbolasers

Commit cd2baa18e0ea842cb8e79f6a2674fcaae9852f01, PR #1453.

These clips show real GameHost, ObserverCam, weapon prefabs, and projectiles firing in a scripted empty sector with the IllustratedBlue locale. They use Unity's native Game View at 1280 by 720. Each capture contains 101 frames with a 0.02-second simulation step. MP4 playback is 50 FPS; GIF playback retains every second frame at 25 FPS, preserving normal game speed.

Rippers fire 28 alternating single slugs during the 1.4-second firing window. Twenty consecutive intervals span approximately 1 second. The magazine contains 48 individual rounds, retaining the former 24 two-slug bursts' bullet capacity; reload remains 1.5 seconds. All six static pale-gold shapes were observed. The projectile atlas is unchanged from v3.

Turbolasers fire 19 synchronized pairs during the same firing window. Sixteen consecutive intervals span approximately 1.2 seconds, preserving the configured 0.075-second interval. Damage and heat per firing event remain unchanged; each event emits two full-damage bolts and spends heat once.

The original scheduler failed the new sustained 0.05-second test because per-shot rounding accumulated. It mostly produced 0.06-second gaps before shifting to 0.04 seconds in that run. The corrected scheduler retains its deadline during sustained fire and re-anchors after idle. The audio check then exposed duplicate gap inference; Ripper audio now observes the cooldown's decision. These are rung-1 fixes that keep each timing fact at its owner.

Validation: 145 focused EditMode/PlayMode tests passed, including 0.1/0.05/0.075-second cadence, pause/resume, reload, heat, muzzle positions, damage, ammo, hangar descriptions, and real FMOD FirstShot values. The separate two-weapon capture test passed. ReSharper passed with zero blocking findings and eight report-only findings on unchanged HangarScreen lines. Independent quality review found no actionable issues.

The capture checks projectile counts and visible brightening at projected positions. It does not assert a warm-color pixel threshold; the earlier v3 capture's color-threshold failure remains in the PR's historical record. These are scripted firing clips, not a full combat playthrough. The full merge test suite has not run, and this PR has not been merged.

The owner's main Unity Editor, PID 30920, remained open. The task's Unity process exited and left no project owner, boot lease, or queued request.
