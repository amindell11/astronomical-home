# Bastion elite (BulwarkElite) ram vs Crimson Aggressor: main vs PR #1724

Setup: `EliteJitterProbe` capture scenario (in the tarball as `EliteJitterProbe.cs.txt`), `ELITE_FILM_FACTION=Bastion`.
BastionBoss profile on BulwarkElite, hull x20, against a Crimson Aggressor with hull x50, framing minHalfHeight 9 / padding 4.
Each run is 2500 fixed ticks (50 s). Main fired only one ram in 30 s, so both runs were lengthened to 50 s to get two or more rams.

- main: `da0c06998d145eedfe8f9b1e39f527858a64c197` (`FliesNavigatorHeading => !Braking`)
- pr1724: `origin/task/bulwark-aim-between-rams` @ `254d0b015c4d28ae1b381e0e6a5fb4905bf6ee37` (`FliesNavigatorHeading => RamLive`)

A hit means RamDash `resolved` went true, i.e. the ram landed contact. "Blink" means the Crimson's position jumped more than 3 units in one tick. Aim error is the signed angle from `Kinematics.Forward` to the target's centre.

| | main | pr1724 |
|---|---|---|
| ram launches / 50 s | 2 | 5 |
| hits | 0 | 2 |
| time between launches (s) | 24.0 | 5.9, 18.9, 8.1, 3.7 |
| aim error at launch (deg) | +11.7, -128.2 | -27.0, -3.0, -14.0, -9.5, -16.5 |
| distance at launch (u) | 6.2, 7.2 | 9.6, 7.1, 7.4, 3.2, 6.3 |
| servo owns yaw (all ticks) | 1.6 % | 87.6 % |
| servo owns yaw, outside brake/ram | 0 % | 92 % |
| mean abs aim error, overall | 45.4 | 37.2 |
| mean abs aim error, outside brake/ram | 44.5 | 37.8 |
| mean abs aim error, during brake | 62.0 (one brake lost the target to a blink) | 9.3 |

Per launch:

- main #1 (t 16.7): launch aim +11.7 deg, 6.2 u. Glanced past and missed (closest 1.7 u). The Crimson blinked 21 ticks later.
- main #2 (t 40.6): the Crimson blinked 17 ticks before launch, during the brake. The ram fired at -128 deg into empty space.
- pr1724 #1 (t 2.8): HIT 0.34 s after launch, -128 target HP.
- pr1724 #2: miss. The Crimson blinked 5 ticks after launch.
- pr1724 #3: miss, closest 2.8 u, no blink.
- pr1724 #4: HIT 0.26 s after launch, -66 target HP.
- pr1724 #5: miss, closest 4.3 u.

What the numbers show:

- On main the servo only aims during the 0.4 s brake. Between rams nobody holds the nose on the target, so mean aim error stays around 45 deg. The AI's sight test (within 15 deg of the nose) is rarely met, which gives 24 s between rams.
- On #1724 the servo holds the nose between rams, so the ram is offered far more often: 5 launches against 2, with gaps as short as the 3.5 s hit cooldown.
- The Crimson often blinks 4 to 7 ticks after a launch, in both variants, so it reads as a reactive dodge. That blink accounts for most misses.
- On #1724, aim error often grows during the brake: brake start to launch went -1.2 to -27, +2.5 to -14, and +1.5 to -16.5. The nose is on target when the brake starts and drifts off as the Crimson moves.

Extra data point, older base (origin/task/elite-inertia-scale @ 674bf6fd5, 1500 ticks, CSVs in `extra-inertia-scale-base/`):

- As-is: 2 launches, 0 hits, 24.0 s apart, servo 2.6 %.
- With a scratch `=> RamLive` edit: 4 launches, 1 hit, gaps 9.3 / 3.5 / 7.2 s, servo 85 %.

The same pattern holds there.

Clips: `bulwark-ram-{main,pr1724}-{realtime,slowmo}.mp4`. Realtime plays at 50 fps; slow-mo uses the capture's unscaled timing, about 3x slower.
