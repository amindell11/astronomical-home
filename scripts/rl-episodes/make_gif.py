#!/usr/bin/env python3
"""Assemble RL-episode frame dumps into GIFs.

The harness writes frames to results/rl-episodes/frames/<stamp>-epNN/ when
record.flag is present (see RLEpisodePlayModeTests). Frames are sampled every
0.1 s of sim time, so --fps 10 plays back in real time.

    python scripts/rl-episodes/make_gif.py results/rl-episodes/frames/<stamp>-ep* [--fps 10] [--scale 0.5]
"""
import argparse
import glob
import os
import sys

from PIL import Image


def make_gif(frame_dir, fps, scale):
    frames = sorted(glob.glob(os.path.join(frame_dir, "f_*.png")))
    if not frames:
        print(f"skip {frame_dir}: no frames", file=sys.stderr)
        return None

    images = []
    for path in frames:
        image = Image.open(path).convert("RGB")
        if scale != 1.0:
            image = image.resize(
                (max(1, int(image.width * scale)), max(1, int(image.height * scale))),
                Image.LANCZOS)
        images.append(image.quantize(colors=128))

    out_path = frame_dir.rstrip("/\\") + ".gif"
    images[0].save(out_path, save_all=True, append_images=images[1:],
                   duration=int(1000 / fps), loop=0, optimize=True)
    print(f"{out_path}  {os.path.getsize(out_path) / 1e6:.1f} MB  {len(images)} frames")
    return out_path


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("frame_dirs", nargs="+", help="episode frame directories (globs ok)")
    parser.add_argument("--fps", type=float, default=10.0)
    parser.add_argument("--scale", type=float, default=0.5)
    args = parser.parse_args()

    expanded = []
    for pattern in args.frame_dirs:
        matches = sorted(glob.glob(pattern))
        expanded.extend(m for m in matches if os.path.isdir(m))
    if not expanded:
        sys.exit("no frame directories matched")

    for frame_dir in expanded:
        make_gif(frame_dir, args.fps, args.scale)


if __name__ == "__main__":
    main()
