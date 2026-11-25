# Ship art stage 1: concept

## Inputs

- The ship's issue: its role in the roster and any owner notes.
- One of two entries: **from scratch**, or **from an existing model or image** (e.g.
  `art/ships/nightshade/concepts/` for Nightshade's new hull).
- The house style: the shipped ships, seen at game scale.

## Commands

- Images: `art/tools/imagegen/README.md`. Pass every reference with its role stated in the
  prompt; each image keeps its provenance sidecar.
- From an existing model: render its top and side views and use them as the references.

## Done

- One design, approved as a **top view plus a turnaround** (side, front, three-quarter).
  The side profile is part of it.
- The approved views are committed to `art/ships/<name>/concepts/`.
- Every round, prompts and sidecars included, is on `evidence/<name>` under `concept/`.

## Owner's gate

The owner approves the views in chat. Rounds are uncapped; the owner decides when to
sketch or redline. Ask for redlines as image coordinates: they turn taste into exact
edits.

## Forbidden moves

- **Geometry before approval.** Blockout starts from approved views only; on past ships,
  modelling ahead of the concept or without an agreed side profile forced rebuilds.
- **Unapproved rounds on main.** They go to the evidence branch.
