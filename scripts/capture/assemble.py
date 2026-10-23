#!/usr/bin/env python3
"""Assemble capture frame dumps into mp4 (default) or gif clips.

The capture module writes <outputRoot>/frames/<stamp>-<clip>/ holding f_%05d.png,
manifest.json (dims, capture cadence, suggested fps), provenance.json, and
phases.csv (frame, unscaled_seconds, sim_seconds, plus any phase columns).

Timing basis: when phases.csv has at least one unscaled_seconds row per PNG and
--fps is omitted, each output frame lasts the unscaled time that passed while
it was shown. A phase log that leads the PNGs (the recorder skips the frames
it spends starting) drops those leading rows. A --step groups the rows it
keeps. --fps replaces that basis, for a faster watch cut. GIF frame durations
are then quantised to 10 ms, which is the unit GIF stores. The read-back
expects that quantised total.

GIF colour: each frame is quantised with Pillow's maximum-coverage palette
(default 256 colours). The read-back decodes the GIF and, on frames that
contain a small saturated subject, fails when the mean per-channel absolute
error against the source exceeds 15. Median-cut loses that subject; this
quantiser keeps it.

    python scripts/capture/assemble.py results/capture/frames/<stamp>-* [--format gif] [--scale 0.5]

--web is the chat/artifact preset: mp4 downscaled to <=854 px wide at crf 30.

The frame dir is this step's intermediate: a written clip is decoded back and
checked (mp4: decoded frame count, and duration when the unscaled basis was
used; gif: quantised playback length and subject colour), and only then is
the frame dir deleted. --keep-frames leaves it in place when the raw frames
are the deliverable.

Exit codes: 0 = every matched directory encoded and verified; nonzero = the
message on stderr names the directory and whether the encode or the read-back
failed. A clip that did not verify keeps both itself and its frames on disk.

mp4 needs the imageio-ffmpeg wheel (bundles ffmpeg, not ffprobe). The repo venvs
are uv-managed with no pip module: uv pip install --python <venv-python> imageio-ffmpeg
gif needs Pillow.
"""
import argparse
import csv
import glob
import json
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
    try:
        with open(manifest_path, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def load_unscaled_times(frame_dir):
    path = os.path.join(frame_dir, "phases.csv")
    if not os.path.isfile(path):
        return None
    with open(path, encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    if not rows or "unscaled_seconds" not in rows[0]:
        sys.exit(f"{path} has no unscaled_seconds column")
    try:
        return [float(row["unscaled_seconds"]) for row in rows]
    except ValueError as error:
        sys.exit(f"{path} has a non-numeric unscaled_seconds: {error}")


def durations_from_times(times, fallback):
    if len(times) < 2:
        return [fallback]
    deltas = [times[i + 1] - times[i] for i in range(len(times) - 1)]
    if any(delta <= 0 for delta in deltas):
        sys.exit("unscaled timestamps must increase")
    deltas.append(deltas[-1])
    return deltas


def stepped(durations, step):
    grouped = []
    for index in range(0, len(durations), step):
        grouped.append(sum(durations[index:index + step]))
    return grouped


def gif_frame_ms(seconds):
    units = int(round(float(seconds) * 1000.0 / GIF_QUANTUM_MS))
    return max(GIF_QUANTUM_MS, units * GIF_QUANTUM_MS)


def write_concat_list(list_path, frames, durations):
    with open(list_path, "w", encoding="utf-8") as f:
        for frame, duration in zip(frames, durations):
            f.write("file '%s'\n" % os.path.abspath(frame).replace("\\", "/"))
            f.write("duration %.6f\n" % duration)
        # concat demuxer ignores the last entry's duration unless the file is repeated
        f.write("file '%s'\n" % os.path.abspath(frames[-1]).replace("\\", "/"))
    return len(frames) + 1


def decode_clip(ffmpeg_exe, clip_path):
    """Decoded frame count and container duration, or the reason ffmpeg failed."""
    cmd = [ffmpeg_exe, "-hide_banner", "-nostats", "-i", clip_path, "-map", "0:v:0", "-f", "null", "-"]
    result = subprocess.run(cmd, capture_output=True)
    stderr = result.stderr.decode(errors="replace")
    if result.returncode != 0:
        return None, None, "ffmpeg could not decode it:\n" + stderr[-2000:]
    counts = re.findall(r"frame=\s*(\d+)", stderr)
    if not counts:
        return None, None, "ffmpeg reported no frame count:\n" + stderr[-2000:]
    duration = re.search(r"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", stderr)
    seconds = None
    if duration:
        hours, minutes, secs = duration.groups()
        seconds = int(hours) * 3600 + int(minutes) * 60 + float(secs)
    return int(counts[-1]), seconds, None


def assemble_mp4(frame_dir, frames, durations, scale, crf, check_duration):
    import imageio_ffmpeg

    ffmpeg_exe = imageio_ffmpeg.get_ffmpeg_exe()
    out_path = frame_dir.rstrip("/\\") + ".mp4"
    list_path = out_path + ".frames.txt"
    expected = write_concat_list(list_path, frames, durations)
    out_fps = len(frames) / sum(durations)

    cmd = [
        ffmpeg_exe, "-y",
        "-f", "concat", "-safe", "0", "-i", list_path,
        "-vf", "scale=trunc(iw*%s/2)*2:trunc(ih*%s/2)*2" % (scale, scale),
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
        "-crf", str(crf),
    ]
    # -r resamples to a constant rate and pads the container. Unscaled timing
    # keeps the concat durations instead.
    if check_duration:
        cmd.extend(["-fps_mode", "vfr"])
    else:
        cmd.extend(["-r", "%.6f" % out_fps])
    cmd.append(out_path)
    try:
        subprocess.run(cmd, check=True, capture_output=True)
    except subprocess.CalledProcessError as error:
        sys.exit("ffmpeg failed:\n" + error.stderr.decode(errors="replace")[-2000:])
    finally:
        os.remove(list_path)

    decoded, duration, failure = decode_clip(ffmpeg_exe, out_path)
    if failure is not None:
        sys.exit(f"read-back of {out_path} failed; frames kept in {frame_dir}: {failure}")
    # Floor, not equality: -r pads the concat stream to constant rate at some fps.
    if decoded < expected:
        sys.exit(f"read-back of {out_path} decoded {decoded} frames, expected at least {expected}; "
                 f"clip and frames kept in {frame_dir}")
    if check_duration:
        expected_seconds = sum(durations)
        if duration is None:
            sys.exit(f"read-back of {out_path} reported no duration; frames kept in {frame_dir}")
        if abs(duration - expected_seconds) > max(0.05, durations[-1]):
            sys.exit(f"read-back of {out_path} plays {duration:.3f} s, expected {expected_seconds:.3f} s; "
                     f"clip and frames kept in {frame_dir}")
    return out_path


def subject_channel_error(source, decoded):
    """Mean per-channel absolute error over small saturated subject pixels, or None."""
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


def assemble_gif(frame_dir, frames, durations, scale, colors):
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
    frame_ms = [gif_frame_ms(duration) for duration in durations]
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
        if written.n_frames != len(sources):
            sys.exit(f"read-back of {out_path} stored {written.n_frames} frames, expected {len(sources)}; "
                     f"clip and frames kept in {frame_dir}")
        for index, source in enumerate(sources):
            written.seek(index)
            error = subject_channel_error(source, written.convert("RGB"))
            if error is not None and error > SUBJECT_CHANNEL_ERROR_MAX:
                sys.exit(f"read-back of {out_path} frame {index} subject colour error {error:.2f} "
                         f"exceeds {SUBJECT_CHANNEL_ERROR_MAX}; clip and frames kept in {frame_dir}")
    return out_path


def assemble(frame_dir, args):
    all_frames = sorted(glob.glob(os.path.join(frame_dir, "f_*.png")))
    if not all_frames:
        print(f"skip {frame_dir}: no frames", file=sys.stderr)
        return False
    frames = all_frames[::args.step]

    manifest = load_manifest(frame_dir)
    fps = args.fps or manifest.get("suggestedFps") or 10.0
    times = None if args.fps else load_unscaled_times(frame_dir)
    if times is None:
        base = [1.0 / fps] * len(all_frames)
    else:
        # The recorder skips the frames it spends starting, so the phase log can
        # lead the PNGs. The rows that remain line up with the files on disk.
        if len(times) < len(all_frames):
            sys.exit(f"{frame_dir}: phases.csv has {len(times)} rows for {len(all_frames)} frames")
        if len(times) > len(all_frames):
            times = times[len(times) - len(all_frames):]
        base = durations_from_times(times, 1.0 / fps)
    durations = stepped(base, args.step)
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
        out_path = assemble_mp4(frame_dir, frames, durations, scale, crf, check_duration=times is not None)
    else:
        out_path = assemble_gif(frame_dir, frames, durations, scale, args.colors)
    out_fps = len(frames) / sum(durations)
    print(f"{out_path}  {os.path.getsize(out_path) / 1e6:.1f} MB  {len(frames)} frames @ {out_fps:g} fps")
    if not args.keep_frames:
        shutil.rmtree(frame_dir)
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("frame_dirs", nargs="+", help="capture frame directories (globs ok)")
    parser.add_argument("--format", choices=("mp4", "gif"), default="mp4")
    parser.add_argument("--fps", type=float, default=None,
                        help="override the unscaled timing basis with this playback fps of the full cadence")
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
