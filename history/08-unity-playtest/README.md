# First standard-laser Unity playtest

The approved B1 source and 340 ms animation are wired to the standard laser on implementation commit `330bc3b8e`. Turbo lasers remain unwired. Initial visible fan length is 1.2 world units, pending the owner's flight feedback.

## What do these images show?

`ship-impact.png` and `asteroid-impact.png` show actual laser collisions against the Crimson ship and a spawned asteroid. The effect comes from the production projectile hit event. The probe holds the particle animation at 50 ms for inspection and uses the game camera's culling mask. The matching `*-without.png` images hide only the impact renderer. The impact changes 14,228 ship-view pixels and 12,714 asteroid-view pixels by more than 25 summed RGB levels.

The probe sets its shot's damage scale to zero to keep each target intact. This is verification setup only. The production change does not alter damage or collision geometry. These are close render checks, not footage of a human flying or approval of world scale.

## Which checks passed?

On 2026-11-06, Unity 6000.1.8f1 passed three `LaserHitVfxPlayModeTests` checks covering contact and direction, expiry and pool reuse, presentation suppression, and TurboLaser exclusion. Two temporary `LaserImpactRenderCheck` checks passed against the real ship and asteroid, including the visible-pixel comparison above. The probe source is preserved here and excluded from the production PR.

The atlas rebaked from approved sources has SHA-256 `48b2c066a9c33605795d98669ce3c0cf9ad9e73c97cb5b0735ff64b1d6d2bdc6`. `bake.json` records its 20-frame layout and lifetime.

`LaserPlaytestBuild.cs` is the temporary batch build entry used for the Windows player. Place it under an Editor folder only while building through the Unity access coordinator, with `-executeMethod Combat.Projectiles.Visual.LaserPlaytestBuild.Build -laserPlaytestOutput <absolute-exe-path>`. It is not production source.
