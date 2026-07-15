#!/usr/bin/env python3
"""Assemble capture frame dumps into mp4 (default) or gif clips.

CaptureRecorder writes <outputRoot>/frames/<stamp>-<clip>/ holding f_%05d.png
plus a manifest.json (dims, capture cadence, suggested fps). --fps defaults
from that manifest so clips play back in real time; --step N drops to every
Nth frame while keeping real-time playback.

    python scripts/capture/assemble.py results/capture/frames/<stamp>-* [--format gif] [--scale 0.5]

mp4 needs the imageio-ffmpeg wheel (bundles ffmpeg): pip install imageio-ffmpeg
"""
import argparse
import glob
import json
import os
import subprocess
import sys


def load_suggested_fps(frame_dir):
    manifest_path = os.path.join(frame_dir, "manifest.json")
    try:
        with open(manifest_path, encoding="utf-8") as f:
            return float(json.load(f)["suggestedFps"])
    except (OSError, KeyError, ValueError):
        return None


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

    fps = args.fps or load_suggested_fps(frame_dir) or 10.0
    out_fps = fps / args.step
    if args.format == "mp4":
        out_path = assemble_mp4(frame_dir, frames, out_fps, args.scale, args.crf)
    else:
        out_path = assemble_gif(frame_dir, frames, out_fps, args.scale, args.colors)
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
    parser.add_argument("--scale", type=float, default=1.0)
    parser.add_argument("--crf", type=int, default=20, help="mp4 quality (lower = better)")
    parser.add_argument("--colors", type=int, default=128, help="gif palette size")
    args = parser.parse_args()
    if args.step < 1:
        parser.error("--step must be >= 1")

    expanded = []
    for pattern in args.frame_dirs:
        expanded.extend(m for m in sorted(glob.glob(pattern)) if os.path.isdir(m))
    if not expanded:
        sys.exit("no frame directories matched")

    for frame_dir in expanded:
        assemble(frame_dir, args)


if __name__ == "__main__":
    main()
