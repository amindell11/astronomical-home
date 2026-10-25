#!/usr/bin/env python3
"""Assemble capture frame dumps into mp4 (default) or gif clips.

The producer's manifest names frames.csv: contiguous one-based frame_id, exact
filename, unscaled_seconds, sim_seconds, then optional fixed numeric measurements.
Completed captures require exactly one matching row per PNG, finite values, and
strictly increasing unscaled timestamps. Validation precedes encoding, even with
--fps. Playback uses observed unscaled intervals; the final hold repeats the last
interval (one frame: 0.1 s). --step N sums skipped intervals. --fps overrides
playback timing. Legacy folders without frameMetadata use suggestedFps or 10 Hz.
GIF durations round to the nearest 10 ms, the unit the format stores. The
maximum-coverage palette preserves small saturated subjects. Read-back rejects
subject colour error above 15 mean RGB levels. MP4 input timestamps use 1 ms
precision so faster-than-25-Hz source frames retain distinct timestamps.

    python scripts/capture/assemble.py results/capture/frames/<stamp>-* [--format gif] [--scale 0.5]

--web is the chat/artifact preset: mp4 downscaled to <=854 px wide at crf 30.

The frame dir is this step's intermediate: a written clip is decoded back and
checked to hold the frames handed to the encoder (mp4: decoded frame count; gif:
quantised playback length and subject colour), and only then is the frame dir
deleted. --keep-frames leaves it in place when the raw frames are the deliverable.

Exit codes: 0 = every matched directory encoded and verified; nonzero = the
message on stderr names the directory and whether the encode or the read-back
failed. A clip that did not verify keeps both itself and its frames on disk.

mp4 needs the imageio-ffmpeg wheel (bundles ffmpeg, not ffprobe). The repo venvs
are uv-managed with no pip module: uv pip install --python <venv-python> imageio-ffmpeg
"""
import argparse
import csv
import glob
import json
import math
import os
import re
import shutil
import subprocess
import sys

WEB_MAX_WIDTH = 854
WEB_CRF = 30
GIF_QUANTUM_MS = 10
SUBJECT_CHANNEL_ERROR_MAX = 15.0


def load_manifest(frame_dir):
    manifest_path = os.path.join(frame_dir, "manifest.json")
    if not os.path.exists(manifest_path):
        return {}
    with open(manifest_path, encoding="utf-8") as f:
        return json.load(f)


def frame_intervals(frame_dir, frames, manifest):
    name = manifest.get("frameMetadata")
    if name is None:
        return None
    if name != os.path.basename(name) or not name:
        raise ValueError("frameMetadata must name a file in the frame directory")
    if manifest.get("completed") is not True or manifest.get("frameCount") != len(frames):
        raise ValueError("completed manifest frameCount must match the PNG count")
    all_pngs = sorted(glob.glob(os.path.join(frame_dir, "*.png")))
    if frames != all_pngs:
        raise ValueError("unexpected PNG outside the producer's frame sequence")
    with open(os.path.join(frame_dir, name), encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream)
        fields = reader.fieldnames
        required = ["frame_id", "filename", "unscaled_seconds", "sim_seconds"]
        if not fields or fields[:4] != required or len(fields) != len(set(fields)):
            raise ValueError("frame metadata requires frame_id,filename,unscaled_seconds,sim_seconds")
        rows = list(reader)
    if len(rows) != len(frames):
        raise ValueError("frame metadata requires exactly one row per PNG")
    times = []
    for index, (row, frame) in enumerate(zip(rows, frames), 1):
        if row.get("frame_id") != str(index) or row.get("filename") != os.path.basename(frame):
            raise ValueError(f"frame metadata row {index} does not match its PNG identity")
        if os.path.basename(frame) != f"f_{index:05d}.png":
            raise ValueError(f"PNG sequence is not contiguous at frame {index}")
        if None in row or any(not math.isfinite(float(row[field])) for field in fields[2:]):
            raise ValueError(f"frame metadata row {index} has invalid numeric values")
        times.append(float(row["unscaled_seconds"]))
    intervals = [end - start for start, end in zip(times, times[1:])]
    if any(interval <= 0 for interval in intervals):
        raise ValueError("unscaled frame timestamps must strictly increase")
    return intervals + [intervals[-1] if intervals else 0.1]


def gif_frame_ms(seconds):
    units = int(round(float(seconds) * 1000.0 / GIF_QUANTUM_MS))
    return max(GIF_QUANTUM_MS, units * GIF_QUANTUM_MS)


def write_concat_list(list_path, frames, out_fps, durations=None):
    with open(list_path, "w", encoding="utf-8") as f:
        for frame, duration in zip(frames, durations or [1.0 / out_fps] * len(frames)):
            f.write("file '%s'\n" % os.path.abspath(frame).replace("\\", "/"))
            # PNG demuxers default to 25 Hz and collapse shorter input intervals.
            f.write("option framerate 1000\n")
            f.write("duration %.6f\n" % duration)
        # concat demuxer ignores the last entry's duration unless the file is repeated
        f.write("file '%s'\n" % os.path.abspath(frames[-1]).replace("\\", "/"))
        f.write("option framerate 1000\n")
    return len(frames) + 1


def decoded_frame_count(ffmpeg_exe, clip_path):
    """Frames ffmpeg actually decodes from the clip, or the reason it could not."""
    cmd = [ffmpeg_exe, "-hide_banner", "-nostats", "-i", clip_path, "-map", "0:v:0", "-f", "null", "-"]
    result = subprocess.run(cmd, capture_output=True)
    stderr = result.stderr.decode(errors="replace")
    if result.returncode != 0:
        return None, "ffmpeg could not decode it:\n" + stderr[-2000:]
    counts = re.findall(r"frame=\s*(\d+)", stderr)
    if not counts:
        return None, "ffmpeg reported no frame count:\n" + stderr[-2000:]
    return int(counts[-1]), None


def assemble_mp4(frame_dir, frames, out_fps, scale, crf, durations=None):
    import imageio_ffmpeg

    ffmpeg_exe = imageio_ffmpeg.get_ffmpeg_exe()
    out_path = frame_dir.rstrip("/\\") + ".mp4"
    list_path = out_path + ".frames.txt"
    expected = write_concat_list(list_path, frames, out_fps, durations)

    cmd = [
        ffmpeg_exe, "-y",
        "-f", "concat", "-safe", "0", "-i", list_path,
        "-vf", "scale=trunc(iw*%s/2)*2:trunc(ih*%s/2)*2" % (scale, scale),
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
        "-crf", str(crf),
    ]
    cmd += ["-fps_mode", "vfr", "-video_track_timescale", "1000000"] if durations else ["-r", "%.6f" % out_fps]
    cmd.append(out_path)
    try:
        subprocess.run(cmd, check=True, capture_output=True)
    except subprocess.CalledProcessError as error:
        sys.exit("ffmpeg failed:\n" + error.stderr.decode(errors="replace")[-2000:])
    finally:
        os.remove(list_path)

    decoded, failure = decoded_frame_count(ffmpeg_exe, out_path)
    if failure is not None:
        sys.exit(f"read-back of {out_path} failed; frames kept in {frame_dir}: {failure}")
    # Floor, not equality: -r pads the concat stream to constant rate at some fps.
    if decoded < expected or (durations and decoded != expected):
        sys.exit(f"read-back of {out_path} decoded {decoded} frames, expected at least {expected}; "
                 f"clip and frames kept in {frame_dir}")
    if durations:
        probe = subprocess.run([ffmpeg_exe, "-i", out_path, "-f", "null", "-"], capture_output=True)
        reported = re.findall(r"time=(\d+):(\d+):([\d.]+)", probe.stderr.decode(errors="replace"))
        actual = sum(float(part) * factor for part, factor in zip(reported[-1], (3600, 60, 1))) if reported else None
        if probe.returncode or actual is None or abs(actual - sum(durations)) > 0.02:
            sys.exit(f"read-back of {out_path} duration {actual} differs from {sum(durations):.6f}s; "
                     f"clip and frames kept in {frame_dir}")
    return out_path


def subject_channel_error(source, decoded):
    if source.size != decoded.size:
        from PIL import Image
        source = source.resize(decoded.size, Image.LANCZOS)
    src = list(source.convert("RGB").getdata())
    dec = list(decoded.convert("RGB").getdata())
    total = 0.0
    count = 0
    for (red, green, blue), (red2, green2, blue2) in zip(src, dec):
        hi = max(red, green, blue)
        lo = min(red, green, blue)
        mid = red + green + blue - hi - lo
        if hi > 90 and hi > 1.3 * mid and hi > 1.1 * lo:
            total += abs(red - red2) + abs(green - green2) + abs(blue - blue2)
            count += 1
    if count < 20:
        return None
    return (total / count) / 3.0


def assemble_gif(frame_dir, frames, out_fps, scale, colors, durations=None):
    from PIL import Image

    sources = []
    palettes = []
    for path in frames:
        image = Image.open(path).convert("RGB")
        if scale != 1.0:
            image = image.resize(
                (max(1, int(image.width * scale)), max(1, int(image.height * scale))),
                Image.LANCZOS)
        sources.append(image)
        palettes.append(image.quantize(colors=colors, method=Image.Quantize.MAXCOVERAGE))

    out_path = frame_dir.rstrip("/\\") + ".gif"
    frame_ms = [gif_frame_ms(duration) for duration in durations] if durations else [gif_frame_ms(1.0 / out_fps)] * len(frames)
    palettes[0].save(out_path, save_all=True, append_images=palettes[1:],
                     duration=frame_ms, loop=0, optimize=False)

    expected_ms = sum(frame_ms)
    with Image.open(out_path) as written:
        decoded_ms = 0
        for index in range(written.n_frames):
            written.seek(index)
            decoded_ms += written.info["duration"]
        if decoded_ms != expected_ms:
            sys.exit(f"read-back of {out_path} plays {decoded_ms} ms, expected {expected_ms} ms; "
                     f"clip and frames kept in {frame_dir}")
        written.seek(0)
        decoded_index = 0
        decoded_end = written.info["duration"]
        source_start = 0
        for index, (source, duration) in enumerate(zip(sources, frame_ms)):
            while source_start >= decoded_end:
                decoded_index += 1
                written.seek(decoded_index)
                decoded_end += written.info["duration"]
            error = subject_channel_error(source, written.convert("RGB"))
            if error is not None and error > SUBJECT_CHANNEL_ERROR_MAX:
                sys.exit(f"read-back of {out_path} frame {index} subject colour error {error:.2f} "
                         f"exceeds {SUBJECT_CHANNEL_ERROR_MAX}; clip and frames kept in {frame_dir}")
            source_start += duration
    return out_path


def assemble(frame_dir, args):
    frames = sorted(glob.glob(os.path.join(frame_dir, "f_*.png")))
    if not frames:
        print(f"skip {frame_dir}: no frames", file=sys.stderr)
        return False

    try:
        manifest = load_manifest(frame_dir)
        intervals = frame_intervals(frame_dir, frames, manifest)
    except (OSError, ValueError, TypeError, KeyError) as error:
        sys.exit(f"invalid capture contract in {frame_dir}: {error}; frames kept")
    durations = None if intervals is None or args.fps else [sum(intervals[i:i + args.step]) for i in range(0, len(frames), args.step)]
    frames = frames[::args.step]
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
        out_path = assemble_mp4(frame_dir, frames, out_fps, scale, crf, durations)
    else:
        out_path = assemble_gif(frame_dir, frames, out_fps, scale, args.colors, durations)
    timing = f"{sum(durations):.6f}s unscaled playback" if durations else f"{out_fps:g} fps"
    print(f"{out_path}  {os.path.getsize(out_path) / 1e6:.1f} MB  {len(frames)} frames @ {timing}")
    if not args.keep_frames:
        shutil.rmtree(frame_dir)
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("frame_dirs", nargs="+", help="capture frame directories (globs ok)")
    parser.add_argument("--format", choices=("mp4", "gif"), default="mp4")
    parser.add_argument("--fps", type=float, default=None,
                        help="override playback fps (default: exact metadata intervals, then legacy suggestedFps)")
    parser.add_argument("--step", type=int, default=1,
                        help="use every Nth frame; playback stays real-time")
    parser.add_argument("--scale", type=float, default=None,
                        help="resize factor (default 1.0; --web derives it from manifest width)")
    parser.add_argument("--crf", type=int, default=None,
                        help="mp4 quality, lower = better (default 20, or 30 with --web)")
    parser.add_argument("--colors", type=int, default=256, help="gif palette size (maximum coverage)")
    parser.add_argument("--web", action="store_true",
                        help=f"chat/artifact preset: mp4 <= {WEB_MAX_WIDTH} px wide at crf {WEB_CRF}")
    parser.add_argument("--keep-frames", action="store_true",
                        help="leave the frame dir in place after a verified encode (raw frames are the deliverable)")
    args = parser.parse_args()
    if args.step < 1:
        parser.error("--step must be >= 1")
    if args.fps is not None and (not math.isfinite(args.fps) or args.fps <= 0):
        parser.error("--fps must be positive and finite")
    if args.web and args.format == "gif":
        parser.error("--web is an mp4 preset; use --format gif with --scale/--step instead")

    expanded = []
    for pattern in args.frame_dirs:
        expanded.extend(m for m in sorted(glob.glob(pattern)) if os.path.isdir(m))
    if not expanded:
        sys.exit("no frame directories matched")

    # Matching directories that all turn out to be frameless is a failed assembly, not a quiet
    # success: the caller is waiting for a clip and would otherwise be handed exit 0 and nothing.
    assembled = 0
    for frame_dir in expanded:
        if assemble(frame_dir, args):
            assembled += 1
    if assembled == 0:
        sys.exit("no clips assembled: every matched directory was frameless")


if __name__ == "__main__":
    main()
