# Nano Banana image generator

`imagegen.py` generates or edits one image with Google's Nano Banana models
(Gemini API) and writes a provenance sidecar next to it. Agents call it from
Bash; `uv run` installs its pinned dependency on first use.

## Setup (once per machine)

Image output has no free tier, so the key must come from an AI Studio project
with billing enabled. Create the key yourself and store it as a Windows user
environment variable from your own terminal:

```powershell
setx GEMINI_API_KEY "<key>"
```

Running processes don't see `setx`, so restart the Claude app afterwards. Never
paste the key into chat, a file in the repo, a URL or a log.

## Usage

```bash
uv run art/tools/imagegen/imagegen.py \
  --prompt "Image 1 is the object reference: keep its silhouette and panel lines. Paint a three-quarter hero concept ..." \
  --ref art/ships/vanguard/reference/side.png \
  --size 2K --aspect 16:9 \
  --out art/ships/vanguard/experiments/2026-09-22-concepts/hero_01
```

- `--out` takes no extension. The model's returned MIME type picks it, which is
  JPEG today. The sidecar is written to `<out>.json`.
- `--edit <image>` sends that image first and records it as the parent; the
  prompt describes the change.
- `--ref` repeats. The request carries no role for a reference, so the prompt
  must say which image is an object, character or style reference.
  Give every view you have (top and side, not one) and let the references carry
  the design: a prompt that describes the design in words gets a generic ship back.
- `--thinking` is optional; nb2 and lite accept `minimal|high`, pro `low|high`.
- `--model` takes `nb2` (default, `gemini-3.1-flash-image`), `pro`
  (`gemini-3-pro-image`), `lite` (`gemini-3.1-flash-lite-image`, 1K only), or
  a raw model id.
- `--status` is `exploration` (default), `placeholder` or `approved-reworked`;
  change it in the sidecar when an image's role changes.

| Model | Object / character / style refs | Sizes | USD per image (1K / 4K) |
|---|---|---|---|
| `nb2` | 10 / 4 / 3 | 512, 1K, 2K, 4K | 0.067 / 0.151 |
| `pro` | 6 / 5 / – | 1K, 2K, 4K | 0.134 / 0.24 |
| `lite` | 14 / – / – | 1K | 0.0336 / – |

Prices are the standard tier as of 2026-09-22; thinking and input tokens bill
on top.
`gemini-2.5-flash-image` shuts down 2026-10-02, so don't use it.

## Outputs

Write outputs next to their subject, typically
`art/<subject>/experiments/<date>-<slug>/`. Images there are LFS-tracked via
`art/.gitattributes`; sidecars stay plain JSON so they diff. Nothing is
committed for you. Every output carries Google's invisible SynthID watermark.

The sidecar records the output filename, the status, the prompt, the parent
and reference paths with sha256 hashes, the provider, model and interaction id,
the parameters, the UTC timestamp, a per-image cost estimate, the response's
token usage and any text the model returned.
