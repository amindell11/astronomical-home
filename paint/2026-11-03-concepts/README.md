# Faction paint concepts, 2026-11-03 (#1329)

Picks are provisional: the owner gate (palette and regions) had not run. `picks.json` lists every pick.

- `refs/`: flat Workbench top renders (`ship_render --set ortho`), the object references for image gen.
- `round1/<faction>/`: three colour directions per faction, `nb2` and `pro`, with imagegen sidecars. Prompts in `prompts/r1-*`.
- `round2/<faction>/`: glow (engine flame plus shield ring) and elite variants of the round-1 pick. Prompts `prompts/r2-*`.
- `round3/bastion/`: Bastion glow re-rolled with cyan forbidden. Prompt `prompts/r3-*`.
- `sheets/`: contact sheets. `round1-<faction>.png` has today's paint first and a game-scale strip under each concept. `gate1-palettes.png` sets each pick beside flat renders in the new region colours.
- `flat/`: `<ship>-idmap.png` false-colour region maps (with a `.json` legend), `<ship>-<palette>.png` flat region-colour renders, `<ship>-legend.png` per-region tiles. Bulwark's `-coat` renders show the coat colour on the hull, which the flat render can't show because the coat is a painted mask.
- `scripts/`: generators for the rounds, the region sampler, the flat palette render, and the sheet builders (scratch).
- `codex-brief.md`: the brief handed to `codex exec`. `worker/` holds the generator and self-check from the Opus worker that ran the same brief after Codex hit its usage limit.
