---
name: ship-art-pipeline
description: Take a ship from concept to an in-game asset through eight owner-approved stages — concept, blockout, detail model, flight check, UV, paint, breakup, lineup review. Use when making a new ship or changing an existing ship's shape, UVs, paint or breakup.
metadata:
  project: astronomical-home
---

# Ship art pipeline

Read `doc/agents/art-pipeline.md` first: the layout, name mirror, legacy list and who
may change geometry hold at every stage. Tool commands live in
`art/tools/ship/README.md`; stage files name the tool and what to pass.

## Ship art stages

Find the ship's current stage, then open its file.

| # | Stage | Ends on | File |
|---|---|---|---|
| 1 | Concept | approved top view plus turnaround | `stages/1-concept.md` |
| 2 | Blockout | approved proportions, in Blender | `stages/2-blockout.md` |
| 3 | Detail model | approved shape, every part in a role | `stages/3-detail-model.md` |
| 4 | Flight check | the owner flew it; the geometry lock is written | `stages/4-flight-check.md` |
| 5 | UV | `ship_uv` passes; the geometry lock still verifies | `stages/5-uv.md` |
| 6 | Paint | approved paint concept, then masks | `stages/6-paint.md` |
| 7 | Breakup | approved breakup in game | `stages/7-breakup.md` |
| 8 | Lineup review | approved beside the other ships | `stages/8-lineup.md` |

## Owner's gate and geometry lock

- **Owner's gate.** A stage starts only after the owner approves the one before it, in
  chat. Every out-of-order step on past ships cost a rebuild.
- **Geometry lock.** The one check a tool enforces: `ship_lock` writes `lock.json` at
  flight-check approval, and `ship_lock --mode verify` fails on geometry that no longer
  matches it. Every stage after the flight check opens with a passing verify. A shape
  change after the geometry lock goes through `ship_lock --mode reopen` with the owner's
  approval; reopen reports what it invalidates (UVs, masks, breakup pieces), and those
  stages run again.

Show every candidate as a picture (SendUserFile, `display: "render"`). Judge it at game
scale as well as large.

## Forbidden moves

These hold in every stage that opens the `.blend`; each stage file adds its own.

- **"Save as" over the owner's session.** Write to the file the owner has open only
  after they save it.
- **Working from the saved file while the owner has unsaved edits.** Ask them to save
  first.
- **Undoing across the owner's steps.** Undo only your own.
- **Regenerating, applying modifiers to, or merging parts of the source after the owner's
  first hand edit.** Edit the live parts in place.
- **Sibling backup copies.** Commit before a risky edit instead.
