# Standard-laser playtest preparation

The user [paused other laser work](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6329049220) to fly and tune the approved standard-laser effect first. The stage-06 variants remain unapproved.

`bake.cjs` reads the unchanged stage-05 source, settings, and motion engine. It produces a transparent 20-frame texture sheet for the approved 340 ms animation. Each tile is 256 pixels square, arranged in five columns and four rows, starting at the upper left. Contact is at the tile center. The fan points toward negative local Y so a particle aligned with the projectile can send the fan back toward the shooter.

Run `node bake.cjs` with Playwright on `NODE_PATH` and an optional `CHROMIUM_EXECUTABLE`. The script prints one JSON object containing absolute output paths and metadata. `bake.json` describes the texture relative to its own directory. A source-hash mismatch or absent initial effect fails nonzero. It renders at double resolution before canvas downsampling. The atlas was inspected for frame placement and visible decay.

`LaserImpact.png` is a prepared asset, not an imported or verified Unity asset. World size, particle orientation, material, prefab hookup, in-game rendering, and playable-build validation still require a pooled implementation workspace. The pool had nine occupied slots at the last check; no slot was acquired and no Unity editor was started for this task.
