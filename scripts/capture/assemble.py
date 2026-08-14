#!/usr/bin/env python3
"""Assemble capture frame dumps into mp4 (default) or gif clips.

The capture module writes <outputRoot>/frames/<stamp>-<clip>/ holding f_%05d.png
plus a manifest.json (dims, capture cadence, suggested fps). --fps defaults
from that manifest so clips play back in real time; --step N drops to every
Nth frame while keeping real-time playback.

    python scripts/capture/assemble.py results/capture/frames/<stamp>-* [--format gif] [--scale 0.5]

--web is the chat/artifact preset: mp4 downscaled to <=854 px wide at crf 30.

mp4 needs the imageio-ffmpeg wheel (bundles ffmpeg). The repo venvs are
uv-managed with no pip module: uv pip install --python <venv-python> imageio-ffmpeg
"""
import argparse
import glob
import json
import os
import subprocess
import sys

WEB_MAX_WIDTH = 854
WEB_CRF = 30


def load_manifest(frame_dir):
    manifest_path = os.path.join(frame_dir, "manifest.json")
    try:
        with open(manifest_path, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def assemble_mp4(frame_dir, frames, out_fps, scale, crf):
    import imageio_ffmpeg

    out_path = frame_dir.rstrip("/\\") + ".mp4"
    list_path = out_path + ".frames.txt"
    with open(list_path, "w", encoding="utf-8") as f:
        for frame in frames:
            f.write("file '%s'\n" % os.path.abspath(frame).replace("\\", "/"))
            f.write("duration %.6f\n" % (1.0 / out_fps))
        # concat demuxer ignores the last entry's duration unless the file is repeated
        f.write("file '%s'\n" % os.path.abspath(frames[-1]).replace("\\", "/"))

    cmd = [
        imageio_ffmpeg.get_ffmpeg_exe(), "-y",
        "-f", "concat", "-safe", "0", "-i", list_path,
        "-vf", "scale=trunc(iw*%s/2)*2:trunc(ih*%s/2)*2" % (scale, scale),
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
        "-crf", str(crf), "-r", "%.6f" % out_fps,
        out_path,
    ]
    try:
        subprocess.run(cmd, check=True, capture_output=True)
    except subprocess.CalledProcessError as error:
        sys.exit("ffmpeg failed:\n" + error.stderr.decode(errors="replace")[-2000:])
    finally:
        os.remove(list_path)
    return out_path


def assemble_gif(frame_dir, frames, out_fps, scale, colors):
    from PIL import Image

    images = []
    for path in frames:
        image = Image.open(path).convert("RGB")
        if scale != 1.0:
            image = image.resize(
                (max(1, int(image.width * scale)), max(1, int(image.height * scale))),
                Image.LANCZOS)
        images.append(image.quantize(colors=colors))

    out_path = frame_dir.rstrip("/\\") + ".gif"
    images[0].save(out_path, save_all=True, append_images=images[1:],
                   duration=int(1000 / out_fps), loop=0, optimize=True)
    return out_path


def assemble(frame_dir, args):
    frames = sorted(glob.glob(os.path.join(frame_dir, "f_*.png")))[::args.step]
    if not frames:
        print(f"skip {frame_dir}: no frames", file=sys.stderr)
        return

    manifest = load_manifest(frame_dir)
    fps = args.fps or manifest.get("suggestedFps") or 10.0
    out_fps = fps / args.step
    scale, crf = args.scale, args.crf
    if args.web:
        if scale is None:
            width = manifest.get("width")
            scale = min(1.0, WEB_MAX_WIDTH / width) if width else 0.5
        if crf is None:
            crf = WEB_CRF
    scale = 1.0 if scale is None else scale
    crf = 20 if crf is None else crf
    if args.format == "mp4":
        out_path = assemble_mp4(frame_dir, frames, out_fps, scale, crf)
    else:
        out_path = assemble_gif(frame_dir, frames, out_fps, scale, args.colors)
    print(f"{out_path}  {os.path.getsize(out_path) / 1e6:.1f} MB  {len(frames)} frames @ {out_fps:g} fps")


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("frame_dirs", nargs="+", help="capture frame directories (globs ok)")
    parser.add_argument("--format", choices=("mp4", "gif"), default="mp4")
    parser.add_argument("--fps", type=float, default=None,
                        help="playback fps of the full capture cadence (default: manifest suggestedFps)")
    parser.add_argument("--step", type=int, default=1,
                        help="use every Nth frame; playback stays real-time")
    parser.add_argument("--scale", type=float, default=None,
                        help="resize factor (default 1.0; --web derives it from manifest width)")
    parser.add_argument("--crf", type=int, default=None,
                        help="mp4 quality, lower = better (default 20, or 30 with --web)")
    parser.add_argument("--colors", type=int, default=128, help="gif palette size")
    parser.add_argument("--web", action="store_true",
                        help=f"chat/artifact preset: mp4 <= {WEB_MAX_WIDTH} px wide at crf {WEB_CRF}")
    args = parser.parse_args()
    if args.step < 1:
        parser.error("--step must be >= 1")
    if args.web and args.format == "gif":
        parser.error("--web is an mp4 preset; use --format gif with --scale/--step instead")

    expanded = []
    for pattern in args.frame_dirs:
        expanded.extend(m for m in sorted(glob.glob(pattern)) if os.path.isdir(m))
    if not expanded:
        sys.exit("no frame directories matched")

    for frame_dir in expanded:
        assemble(frame_dir, args)


if __name__ == "__main__":
    main()
