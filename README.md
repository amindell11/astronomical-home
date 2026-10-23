# preview-host-recap: evidence for #1152 (Slice-1 of arc #1151)

Filmed 2026-10-23 on slot `agent-5` through the existing scenario runner
(`CaptureScenarioPlayModeTests`, cold windowed editor), frames read at 1/255 with
`compare.py` (a scratch tool, not in the repo).

## parity/

The acceptance check: the breakdown card and banner rendered through the preview host against
the same states rendered by `GameHost` on main (`97a956dae`), at 1280×720 and 1920×1080.

- `main/*.png`: `GameHost` on main over a bare two-sector profile, the observer camera pointed at
  empty space, filmed by `RecapBaselineScenario.cs` (kept here; it ran from `scratch/capture/` and
  is not part of the PR). The run's data is pinned by waiting on the run tally: extraction at six
  seconds, victory at fifteen, death at six on the next run.
- `host/*.png`: `PreviewHost` on the branch over the same bare profile, showing contexts built from
  those same numbers, filmed by the promoted `RecapParityScenario`.
- `parity.md` / `parity.json`: per state and size, the fraction of pixels whose largest channel
  difference is at most 1 (of 255) over the card or banner rectangle, over the frame with the FMOD
  debug overlay masked, and over the whole frame. Every card and banner region and every masked
  frame is 100.000%; the whole-frame residue (about 0.01%) is the FMOD overlay's CPU readout,
  which changes between runs.
- `baseline-run-summary.json`, `host-run-summary.json`: the two runner summaries.

What this does not claim: timing. Both sides were filmed at their hold states; the fades are
checked by `RecapOutcomeSequenceEditModeTests` against main's numbers and by the two PlayMode
fixtures the brief names.

## sheet/

Every fixture context at its card, the first victory and defeat fixtures at their banner, through
the host over `TrialSector` with its rocks and AI ships, at 16:9 (1280×720) and 21:9 (1680×720):
`sheet-16x9.png`, `sheet-21x9.png` (contact sheets) and the stills they tile. `panel.png` is the
control panel window read off the screen during the same run.

## test summaries

From the final chained runs on the committed tree: `editmode-tests-summary.json` (8 passed: the
sequence function and both bootstraps) and `playmode-tests-summary.json` (14 passed:
`RunRecapScreensPlayModeTests`, `MultiSectorRunPlayModeTests`, `GameMusicPlayModeTests`,
`HangarViewPlayModeTests`, the four fixtures that build a presenting host). The death test's press
goes through Unity's isolated input fixture, the change #1146 made for the same raw-input flake; its
checks are unchanged.
