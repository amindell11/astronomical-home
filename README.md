# Missile and mine explosion motion sketches

Revised proposal, 2026-10-21. Related work: #789, with #794 folded into it. The user found the first pass slow and the mine odd-looking, and supplied two Star Wars seismic-charge stills as the intended direction. The stills remain outside this public branch.

## What should distinguish the weapons?

The mine now starts with a blue-white energy flash and two brief opposing flares, then opens into a broad translucent blue disc. A thin bright leading edge carries its expansion. Blue tone bands and a few irregular strokes give the plane structure without the first pass's detached chunks. The interior carries a transparent blue wash instead of an empty hole. An oblique view and an overhead view show the same effect, so the film angle is not confused with the game's camera projection.

The compact warm missile burst runs at 1.7 times the first pass's speed. Fire breaks up by about 180 ms, and sparks and brief smoke clear by about 340 ms. This revision changes its timing, not its shape direction.

The proposed mine sweep lasts 0.36 seconds, with a faint energy tail through 0.48 seconds. Its bright rim ends with the proposed sweep. This is an aesthetic timing proposal, not an implemented gameplay change. The current prefab takes 0.8 seconds to reach 12 units at 15 units per second. A 0.36-second gameplay sweep at the same radius would require about 33.3 units per second. Implementation must keep the visible boundary and damage sweep synchronized; it must not run this faster visual ahead of existing damage.

No extra fuse delay or second pulse is proposed. The current concussion weapon is aimed toward a point and detonates on contact or arrival. Missile expiry feedback remains separate in #1004. The earlier directions remain in this branch's history at dacf7e5542bddbdf6bfbcb958975508cbf5c9593.

## How can the motion be reviewed?

- `missile-mine-explosions.html` is the interactive conversation fragment. Play all defaults to real time; optional slow motion and a time slider expose the poses.
- `preview.html` is its standalone browser version.
- `motion-sketches.gif` shows the same sequence at real time.
- `comparison.png` shows the proposals at 120 ms.

The mine has oblique and overhead views; the missile has an enlarged view and a 96 px comparison box. These views do not establish world scale or final camera size. This is not an in-game capture, finished sprite art or Unity bloom proof.

## What was checked?

The browser preview ran without JavaScript errors. Scrubbing updated the displayed time, playback completed, and the 360 px layout had no horizontal overflow. Large and small views were inspected. Unity and gameplay were not modified or tested.

## Which references informed this pass?

- [Approved drawn explosion](https://github.com/amindell11/astronomical-home/pull/726), especially Why this version and Visual evidence.
- `doc/design/Art/Style.md`.
- `src/Asteroids3D/Assets/Visuals/Vfx/LayeredExplosion/Textures/`.
- `ConcussionWaveVisual`, `ConcussionWave` and `MissileVisual`.

The revised mine follows the user's seismic-charge direction. The particular drawing and timing remain proposals for visual review under `.claude/skills/aesthetic-authoring/SKILL.md`.
