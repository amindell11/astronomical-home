# Round 1 flames on ships

2D composites, not an in-engine capture. Each Codex strip is sliced into frames
and added (additive, as an emissive sprite) onto the player-ship row of
`evidence/factions:paint/2026-11-03-lineup/factions-2500.png`, at the nozzle
positions and lengths of today's flames (found by diffing that still against
`factions-flames-2500.png`). Stepped at 12 fps; each nozzle starts on a
different frame.

- `codex-*-lineup.gif`: the five player ships at capture resolution.
- `closeup-3x-all.gif`: Crimson and Vanguard nozzles, 3x nearest-neighbour zoom
  of the same render (shown at half size), all four strips stacked.
- `anim.py`: the script; it reads `../*.png` and the two faction stills.
