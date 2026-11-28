# Imagegen round 1: engine exhaust (#1785), 2026-11-28

Refs passed to every run, in order: `history/refs/image1-pick-bd2.png` (shape),
`history/refs/image2-blend-sheet.png` (colour/throttle context),
`history/refs/image3-ram-dash-style.png` (style).

| File | Model | Prompt |
|---|---|---|
| codex-a6f-1.png | Codex image generation, `gpt-6-astra` | `prompt-a-6frames.txt` (6-frame strip) |
| codex-a6f-2.png | Codex image generation, `gpt-6-astra` | `prompt-a-6frames.txt` |
| codex-b4f-1.png | Codex image generation, `gpt-6-astra` | `prompt-b-4frames.txt` (4 frames, length 3x width) |
| codex-b4f-2.png | Codex image generation, `gpt-6-astra` | `prompt-b-4frames.txt` |

`codex-task-*.md` is the exact packet sent to `codex exec` (prompt plus save instructions).

Gemini (nb2, pro) was not run: the API key returned 402, prepayment credits depleted.
