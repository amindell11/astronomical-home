# Missile and mine explosion motion sketches

Proposed directions, 2026-10-21. No user selection or implementation approval yet. Related work: #789, with #794 folded into it.

## What should distinguish the weapons?

The recommended pair is missile A, a compact angular fire burst, and mine A, a blue pressure ring with a warm ignition. Both retain the drawn explosion's separate core, sparks and smoke, broad color bands and crisp silhouette. The finished drawings should inherit the irregular contours and sculpted flame shapes of the shipped textures. These procedural sketches only test shape, timing and separation.

The missile should peak quickly and clear quickly, leaving room for a ship destruction effect if the hit kills its target. Starting timing: a bright core for about 60 ms, fire breakup by 310 ms, sparse sparks through about 500 ms and brief smoke through 580 ms. The alternative missile B pushes some splinters along the incoming travel direction. Its blast remains radial. The current visual spawn uses identity rotation, so choosing B would require capturing the projectile's direction for presentation.

The mine should communicate the traveling damage boundary. A short warm ignition clears into a pale blue continuous rim with irregular drawn shapes behind it. The rim follows the gameplay radius, not an independent animation curve. The current prefab expands to 12 units at 15 units per second, taking 0.8 seconds. Any remaining visual tail must be clearly separate from the active damage edge. Mine B keeps the same motion but uses gold and orange, bringing it closer to the fire palette at the cost of less immediate distinction from missiles.

No extra fuse delay, second damage pulse or gameplay change is proposed. The current concussion weapon is aimed toward a point and detonates on contact or arrival. Missile expiry feedback remains separate in #1004.

## How can the motion be reviewed?

- `missile-mine-explosions.html` is the interactive conversation fragment. Play all, quarter speed and a time slider compare four proposals.
- `preview.html` is its standalone browser version.
- `motion-sketches.gif` shows the same sequence at half speed.
- `comparison.png` shows the proposals at 160 ms.

Each effect has an enlarged view and a 96 px comparison box. Effects are framed independently; the boxes do not establish world scale or final camera size. This is not an in-game capture, finished sprite art or bloom proof.

## What was checked?

The browser preview ran without JavaScript errors. Scrubbing updated the displayed time, playback completed, and the 360 px layout had no horizontal overflow. Large and small views were inspected. Unity and gameplay were not modified or tested.

## Which references informed this pass?

- [Approved drawn explosion](https://github.com/amindell11/astronomical-home/pull/726), especially Why this version and Visual evidence.
- `doc/design/Art/Style.md`.
- `src/Asteroids3D/Assets/Visuals/Vfx/LayeredExplosion/Textures/`.
- `ConcussionWaveVisual`, `ConcussionWave` and `MissileVisual`.

The next design decision is the preferred missile and mine shape. Detailed art follows that selection under `.claude/skills/aesthetic-authoring/SKILL.md`.
