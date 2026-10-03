# Hangar and HUD image-generation experiments

Dated image-generation experiments that set the look of the hangar and the combat
view. They are not tied to one ship. Nothing here merges to main. Moved off the
primary tree's untracked `art/gameplay/experiments/` and `art/hangar/experiments/`
under issue #869.

Each image has a `.prompt.txt` and, where the generator wrote one, a `.json` sidecar
(provider, model, inputs). Sidecars name local input paths on the authoring machine.

| Folder | What it explored |
| --- | --- |
| `experiments/hangar/2026-09-22-nano-banana/`, `experiments/hangar/2026-09-22-nano-banana-ships-only/` | First hangar layouts: loadout console, single berth, shared hangar. |
| `experiments/hangar/2026-09-23-style-board/` | Palette and wear variants for the hangar and loadout backdrops. |
| `experiments/hangar/2026-09-23-approved-style/` | The approved hangar style, tight and loose renders. |
| `experiments/gameplay/2026-09-23-hangar-b-style/` | The hangar style carried into a combat frame. |
| `experiments/gameplay/2026-09-23-minimal-symbol-hud/` | Minimal symbol HUD concepts. |
| `experiments/gameplay/2026-09-23-pro-lite-ui/` | Model comparison on dimensional and illustrated UI. |
