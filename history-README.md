# Hangar hologram look: design history

Slice B of the hangar console arc (#1434), issue #1567. The generator and tuner live on main in
`art/ui/hangar-hologram/`.

## Reference

The owner-approved console pass 28: its
[camera render](https://github.com/amindell11/astronomical-home/blob/3b14f5d5a51d86463aba0922497b6377865ab38d/consoles/tube-port-28/console-camera.png)
shows the ship as a see-through painted model under thin white lines, over a soft warm cone.

## The three takes (2026-11-14)

`takes-three.png` shows them on Vanguard, Nightshade and Bulwark, as mock-ups drawn from the game's own
ship buffers over the real hangar plate.

- A, painted glass: the ship keeps its paint, thinned and see-through, under thin cream lines.
- B, projector light: one cyan light and no paint.
- C, line drawing: a dark glass body with cream lines.

The owner picked A: "yeah painted glass is good". `history-takes-tuner.template.html` and
`history-takes.json` are the three-take version of the tuner page and its values.

## In the game

- `before-after-vanguard.png`: the hangar before this slice (the ship in its own paint, no cone) and after.
- `mock-vs-game-vanguard.png`: the tuner page beside the game at the same values.
- `game-five-ships.png`: every offered ship.
- `game-vanguard-1080p.png`: the full 1920×1080 frame.
