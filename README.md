# Missile and mine explosion motion sketches

Rough shapes approved, 2026-10-21. Related work: #789, with #794 folded into it. The user selected the revised seismic-disc mine and compact warm missile, then instructed us to keep the old speed. [Recorded user ruling](https://github.com/amindell11/astronomical-home/issues/789#issuecomment-6322706438). The two supplied Star Wars reference stills remain outside this public branch.

## What should distinguish the weapons?

The mine now starts with a blue-white energy flash and two brief opposing flares, then opens into a broad translucent blue disc. A thin bright leading edge carries its expansion. Blue tone bands and a few irregular strokes give the plane structure without the first pass's detached chunks. The interior carries a transparent blue wash instead of an empty hole. An oblique view and an overhead view show the same effect, so the film angle is not confused with the game's camera projection.

The compact warm missile burst uses the original speed. Fire breaks up by about 310 ms, and sparks and brief smoke clear by about 580 ms.

The mine keeps its original 0.8-second sweep, matching the current 12-unit radius and 15 units per second. The bright rim ends with that sweep; a dim residual wash clears by 0.92 seconds. The faster 0.36-second proposal was rejected. No damage-speed or balance change is needed. Implementation must keep the visible leading edge synchronized with the gameplay radius.

No extra fuse delay or second pulse is proposed. The current concussion weapon is aimed toward a point and detonates on contact or arrival. Missile expiry feedback remains separate in #1004. The earlier directions remain in this branch's history at dacf7e5542bddbdf6bfbcb958975508cbf5c9593.

## How can the motion be reviewed?

- `missile-mine-explosions.html` is the interactive conversation fragment. Play all defaults to real time; optional slow motion and a time slider expose the poses.
- `preview.html` is its standalone browser version.
- `motion-sketches.gif` shows the same sequence at real time.
- `comparison.png` shows the proposals at 120 ms.
- `history/refinement-01/` holds the first drawn art candidates and their prompts. These are static reference sheets, not approved production sprites or new motion.

The mine has oblique and overhead views; the missile has an enlarged view and a 96 px comparison box. These views do not establish world scale or final camera size. This is not an in-game capture, finished sprite art or Unity bloom proof.

## What was checked?

The browser preview ran without JavaScript errors. Scrubbing updated the displayed time, playback completed, and the 360 px layout had no horizontal overflow. Large and small views were inspected. Unity and gameplay were not modified or tested.

## Which references informed this pass?

- [Approved drawn explosion](https://github.com/amindell11/astronomical-home/pull/726), especially Why this version and Visual evidence.
- `doc/design/Art/Style.md`.
- `src/Asteroids3D/Assets/Visuals/Vfx/LayeredExplosion/Textures/`.
- `ConcussionWaveVisual`, `ConcussionWave` and `MissileVisual`.

The shape selection is complete. Refined art remains at the visual review stage under `.claude/skills/aesthetic-authoring/SKILL.md`.
