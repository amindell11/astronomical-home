# Preview tuning targets

> STATUS: living — branch-triggered reference for making something tunable in the preview host; pointed at from `AGENTS.md`.

A *tuning target* is one thing the owner tunes in the preview panel (`Window/Preview Host`): the sector jump, the run recap, the HUD. Each is one editor class beside `TuningTarget` in `Editor/Game/Preview/`, naming its feel profiles and its entries. The panel opens on a target and draws only its knobs.

## What is tunable

A thing is tunable when all three hold:

- **Its knobs live in a feel profile the shipped game references** — a ScriptableObject on the `GameHost` prefab or on a prefab it builds. The panel edits that asset in place under `FeelSnapshot`, and Save writes it. A copy the game never reads tunes nothing.
- **The preview reaches it through the game's own seams** — the shipped `GameHost` prefab for Playtest; a real session, rig and presentation module for Replay.
- **Every game type stays preview-free** — owner ruling 2026-10-30 on #1270. When the editor needs a reference the game keeps private, read it through `SerializedObject`, as an inspector does (`HudTarget.CanopyProfile`). An override field, setter, getter or parameter added for the preview is the smell; stop and raise it with the user, because the fix is a healthier game seam or a staged moment.

## Adding a target

1. Add one editor class beside `HudTarget`: its name, its entries, its profiles resolved from the `GameHost` prefab, and `DrawControls` only for a control no profile owns.
2. Register it in `TuningTarget.All`. The bootstrap snapshots every target's profiles on entry, so Save, Discard and the exit prompt cover it with no further wiring.
3. A new entry goes in `PreviewEntry` and gets classified in `PreviewHost.IsPlaytest`. The host composes entries and stays blind to targets.
4. Pin its entries and profiles in `TuningTargetEditModeTests`.

## Replay or Playtest

- **Replay** — a timeline subject (`IPreviewSubject`) the transport scrubs, loops and holds. Use it when the owner judges a moment: a presentation module that is a function of a feel profile, a context and a time. The subject reaches its state through the real cause (a *staged moment*), such as damage through the real damage path.
- **Playtest** — the shipped `GameHost` prefab, flown by the owner, with preview run records and at most swapped sectors. Use it when the feel shows only under real input and real timing.

A target lists Replay entries above Playtest ones; the top entry is where a switch lands when the new target lacks the running one. Two targets may share an entry (the recap and the HUD share Combat), and switching between them keeps the running world.
