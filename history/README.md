# Keystone effects: concept history (2026-11-16)

Image-model concept rounds for the Dash, Ram, Blink and Overdrive keystone effects (#1574).
Every board was generated with `codex exec`'s built-in image tool (the Gemini key was out of
credit). Each `run_*/` holds the board (`out.webp`) and the exact prompt (`prompt.txt`);
`make_prompts.py` regenerates the prompts for its round.

References handed to every round (`refs/`):
- `*-top.webp`: each ship from directly above under game lighting, captured fresh from the prefabs.
- `codex-embed-0.webp`: the current Vanguard, the same image as `vanguard-top` but cropped.
- `style_frame.webp`: the game's style frame (painterly nebula, ink-outlined ships).

| Round | What it tried | Owner's call |
|---|---|---|
| 1-cel-dash, 2-cel-ram-blink-overdrive | cel-banded effects in the weapon VFX grammar | rejected: "too fire / laser-y" |
| 3-dash-languages | six effect languages on Dash: ink lines, smear, vapour, space ripple, stardust, hard-light panes | picked the panes (`run_panes`) |
| 4-glass | panes taken literally as glass objects (shells, prows, wing extensions, doorways) | rejected: the look was wanted as trails, not as glass |
| 5-trails | the pane look as motion trails only, two takes per keystone | Dash A, Ram A, Overdrive A; Blink rejected because it read as a dash |
| 6-blink-teleport | Blink as a teleport with effects only at the two ends | liked B (pinch doors) and D (destination mark) |
| 7-blink-doors-plus-mark | three ways to combine B and D | picked 3, the closed-door mark |

Chosen frames: `5-trails/run_dash_a`, `5-trails/run_ram_a`, `5-trails/run_od_a`,
`7-blink-doors-plus-mark/run_doors`. In `run_doors` the model redrew the Crimson as a generic
jet; only the effect was judged.
