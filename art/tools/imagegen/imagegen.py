# /// script
# requires-python = ">=3.11"
# dependencies = ["google-genai==2.25.0"]
# ///
"""Generate or edit an image with Google's Nano Banana models and write a provenance sidecar.

Usage: uv run art/tools/imagegen/imagegen.py --prompt "..." --out <path-without-extension> [options]
See README.md next to this file.
"""

import argparse
import base64
import hashlib
import json
import mimetypes
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

MODELS = {
    "nb2": "gemini-3.1-flash-image",
    "pro": "gemini-3-pro-image",
    "lite": "gemini-3.1-flash-lite-image",
}

# Standard (non-batch) USD per output image, ai.google.dev/gemini-api/docs/pricing as of 2026-09-22.
# Thinking tokens bill on top; the sidecar records them from the response's usage block.
PRICE_PER_IMAGE = {
    "gemini-3.1-flash-image": {"512": 0.045, "1K": 0.067, "2K": 0.101, "4K": 0.151},
    "gemini-3-pro-image": {"1K": 0.134, "2K": 0.134, "4K": 0.24},
    "gemini-3.1-flash-lite-image": {"1K": 0.0336},
}
PRICES_AS_OF = "2026-09-22"

STATUSES = ("exploration", "placeholder", "approved-reworked")

EXTENSIONS = {"image/jpeg": ".jpg", "image/png": ".png", "image/webp": ".webp"}


def parse_args(argv):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    prompt = p.add_mutually_exclusive_group(required=True)
    prompt.add_argument("--prompt", help="Prompt text. Name each reference's role (object/character/style) here.")
    prompt.add_argument("--prompt-file", type=Path, help="Read the prompt from a UTF-8 file.")
    p.add_argument("--edit", type=Path, help="Image to edit; sent first and recorded as the parent.")
    p.add_argument("--ref", type=Path, action="append", default=[], help="Reference image, repeatable; sent in order.")
    p.add_argument("--model", default="nb2", help=f"Alias ({', '.join(MODELS)}) or a raw model id. Default nb2.")
    p.add_argument("--size", default="1K", choices=["512", "1K", "2K", "4K"])
    p.add_argument("--aspect", default="1:1", help="Aspect ratio, e.g. 16:9, 3:2, 21:9.")
    p.add_argument("--thinking", default="minimal", choices=["minimal", "high"])
    p.add_argument("--status", default="exploration", choices=STATUSES)
    p.add_argument("--out", type=Path, required=True,
                   help="Output path without extension; the returned MIME type picks it. Sidecar goes to <out>.json.")
    p.add_argument("--force", action="store_true", help="Overwrite an existing output.")
    args = p.parse_args(argv)
    args.prompt_text = args.prompt if args.prompt is not None else args.prompt_file.read_text(encoding="utf-8")
    args.model_id = MODELS.get(args.model, args.model)
    return args


def image_part(path):
    mime = mimetypes.guess_type(path.name)[0]
    if mime is None or not mime.startswith("image/"):
        sys.exit(f"imagegen: {path} is not a recognised image type")
    data = path.read_bytes()
    part = {"type": "image", "data": base64.b64encode(data).decode("ascii"), "mime_type": mime}
    return part, {"path": path.as_posix(), "sha256": hashlib.sha256(data).hexdigest()}


def main(argv):
    args = parse_args(argv)
    key = os.environ.get("GEMINI_API_KEY")
    if not key:
        sys.exit("imagegen: GEMINI_API_KEY is not set (see art/tools/imagegen/README.md)")

    images = ([args.edit] if args.edit else []) + args.ref
    for path in images:
        if not path.is_file():
            sys.exit(f"imagegen: no such image: {path}")
    parts, records = zip(*(image_part(p) for p in images)) if images else ((), ())

    sidecar_path = args.out.with_name(args.out.name + ".json")
    if sidecar_path.exists() and not args.force:
        sys.exit(f"imagegen: {sidecar_path} exists; pass --force to overwrite")

    from google import genai

    client = genai.Client(api_key=key)
    started = datetime.now(timezone.utc)
    interaction = client.interactions.create(
        model=args.model_id,
        input=[{"type": "text", "text": args.prompt_text}, *parts],
        response_format={"type": "image", "aspect_ratio": args.aspect, "image_size": args.size},
        generation_config={"thinking_level": args.thinking},
    )
    if interaction.output_image is None or not interaction.output_image.data:
        sys.exit(f"imagegen: no image returned (status={interaction.status}, text={interaction.output_text!r})")

    mime = interaction.output_image.mime_type
    if mime not in EXTENSIONS:
        sys.exit(f"imagegen: unexpected output MIME type {mime!r}")
    out_path = args.out.with_suffix(args.out.suffix + EXTENSIONS[mime])
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(base64.b64decode(interaction.output_image.data))

    usage = interaction.usage.model_dump(exclude_none=True) if interaction.usage else None
    sidecar = {
        "output": out_path.name,
        "status": args.status,
        "provider": "google-gemini-api",
        "model": args.model_id,
        "interaction_id": interaction.id,
        "created_utc": started.isoformat(timespec="seconds"),
        "prompt": args.prompt_text,
        "parent": records[0] if args.edit else None,
        "references": list(records[1:] if args.edit else records),
        "params": {"image_size": args.size, "aspect_ratio": args.aspect, "thinking_level": args.thinking},
        "cost_estimate_usd": {
            "per_image": PRICE_PER_IMAGE.get(args.model_id, {}).get(args.size),
            "prices_as_of": PRICES_AS_OF,
            "excludes": "thinking and input tokens",
        },
        "usage": usage,
        "model_text": interaction.output_text,
    }
    sidecar_path.write_text(json.dumps(sidecar, indent=2) + "\n", encoding="utf-8")
    print(out_path.as_posix())
    print(sidecar_path.as_posix())


if __name__ == "__main__":
    main(sys.argv[1:])
