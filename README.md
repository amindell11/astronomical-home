# Hangar look, 2026-11-16

Rounds behind the hangar direction the owner settled on 2026-11-16. The write-up is on #684, and the build proposal is on #1434.

## Where it landed

- `hangar-final-still.png`: the settled hangar, rendered in Cycles at full resolution.
- `entry-animatic-final.mp4`: the settle-in and power-on entry with the hologram growing from the lens. It's an EEVEE draft at 60% resolution, with no sound.
- `entry-animatic-final-sheet.png`: key frames from that animatic, labelled by time.

## Codex image rounds (abandoned)

The owner found every concept too far from the shipped console, so the work moved to Blender. Each round has a sheet and the exact prompts sent to codex's image tool (`<round>-prompts.txt`: the shared preamble, then one line per variant).

- `codex-r1`: six single fixes (sky, hologram, instruments, dock, combined, night shift). Pick: e, all fixes combined.
- `codex-r2`: one change at a time on e. Pick: a (tightened ink style) + d (navy screens) + light grit.
- `codex-r3`: three takes on that mix.
- `codex-r4-radical`: eight radical restyles. Pick: b (1979 freighter) mixed with h (holotable), pulled back toward production.
- `codex-r5-bh`: b + h at three camera angles.
- `codex-r6-pullback`: back toward the modern game (navy screens, house fonts, the real Vanguard), at four levels of hardware dressing.

## Blender rounds

Built on the armoured console from #1521 (`console.blend` at evidence commit 5a099e20af3eef1e2cb78cc3e570ee7fc0cdc28a), at the game camera. Every change is a material, a light or an added part on top of that model.

- `blender-r1-redress-sheet.png`: sky, hologram, lamps and navy screens, one at a time and together. This round used the pass-28 model; the later rounds use the armoured one.
- `blender-r2-layout-sheet.png`: the redress on the armoured model, plus a side-on hologram, a guarded Launch and a Launch lever. Pick: guarded Launch.
- `blender-r2-lamps-before-after.png`: the lamps after the rule "every lamp belongs to a control, on a flat face".
- `blender-r3-guard-pass-sheet.png`, `blender-r3-guard-closeups.png`: the right terminal turned 18° or 10° toward the centre, and the header detail. Pick: 10°.
- `blender-r4-look-sheet.png`: wear and markings, then haze, then launch gates outside the window. Wear and haze were kept. The gates were not.
- `blender-r4-air-wear-dock.png`: the launch gates at full size.
- `blender-r4-entry-storyboards.png`: three entries (power-on, shutter lift, settle in). Pick: settle in combined with power-on.
- `entry-animatic-1-scan.mp4`, `entry-animatic-1-contact.png`: the first animatic. The hologram scans in behind a cyan line, with dust motes and hazard stripes on the guard. The owner liked it but asked for a different hologram entrance, and for no stripes.
- `holo-entrances.mp4`, `holo-entrances-contact.png`: three hologram entrances (trace then fill, grow from the lens, coalesce). Pick: grow from the lens, without the dust motes.

## Reproducing

`dress.py` runs inside Blender 5.1 against the armoured `console.blend`, saved locally as `console-armored.blend`, with `current.png` (the in-game screenshot whose screen content goes on the glass) next to it.

```
MOCK_YAW=10 blender -b console-armored.blend --python dress.py -- out.png sky,holo,lamps,screens,bloom,guard,header,angle,air,wear
```

- Flags pick the layers. `side` also needs pass 28's `console.blend` saved as `console-src.blend`, plus a list of its visible objects, so it won't run from this branch alone.
- `run_animatic.sh <out_dir> 24 6 <procs> 60`, with `EXTRA=holo=grow`, renders the final animatic in parallel EEVEE processes and encodes it.
- `run_holo_clips.sh` and `encode_holo.py` made the entrance comparison.
- Every look value is a named constant at the top of `dress.py`. Every animatic timing lives in `run_animatic` and `holo_apply`.
