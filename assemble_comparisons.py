import json
from pathlib import Path
import shutil
import subprocess

import imageio_ffmpeg
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
OUTPUT = Path(__file__).resolve().parent / "evidence"
FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()

def run(*arguments):
    result = subprocess.run([FFMPEG, "-hide_banner", "-loglevel", "error", "-y", *arguments], capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(result.stderr)

records = []
for locale in (6, 7):
    inputs = []
    sequences = []
    for variant in ("A-gentle", "B-stronger"):
        matches = list((ROOT / "results/capture/frames").glob(f"*-Locale_{locale}-{variant}-v6.mp4"))
        assert len(matches) == 1, matches
        source = matches[0]
        target = OUTPUT / f"Locale_{locale}-{variant}-v6.mp4"
        shutil.copy2(source, target)
        frames = source.with_suffix("")
        sequences.append(frames)
        for suffix, name in (("frames.csv", "frames.csv"), ("manifest.json", "manifest.json"), ("provenance.json", "provenance.json")):
            shutil.copy2(frames / name, OUTPUT / f"Locale_{locale}-{variant}-v6-{suffix}")
        inputs.append(target)
    movie = OUTPUT / f"Locale_{locale}-comparison-v6.mp4"
    font = "C\\:/Windows/Fonts/segoeui.ttf"
    filters = []
    for index, label in enumerate(("A - Gentle", "B - Stronger")):
        filters.append(f"[{index}:v]setpts=PTS-STARTPTS,scale=640:360,pad=640:394:0:34:color=0x0b0e18,drawtext=fontfile='{font}':text='{label}':fontsize=21:fontcolor=white:x=14:y=5[v{index}]")
    filters.append("[v0][v1]hstack=inputs=2[out]")
    run("-framerate", "50/3", "-i", str(sequences[0] / "f_%05d.png"), "-framerate", "50/3", "-i", str(sequences[1] / "f_%05d.png"), "-filter_complex", ";".join(filters), "-map", "[out]", "-an", "-r", "50/3", "-fps_mode", "cfr", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(movie))
    count, seconds = imageio_ffmpeg.count_frames_and_secs(str(movie))
    expected = min(json.loads((path / "manifest.json").read_text(encoding="utf-8-sig"))["frameCount"] for path in sequences)
    assert count == expected, (count, expected)
    gif = movie.with_suffix(".gif")
    run("-i", str(movie), "-filter_complex", "fps=25/4,scale=960:-2,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3", "-loop", "0", str(gif))
    with Image.open(gif) as image:
        duration = 0
        for frame in range(image.n_frames):
            image.seek(frame)
            duration += image.info["duration"]
        assert abs(duration / 1000 - seconds) < .2, (duration, seconds)
    poster = movie.with_suffix(".png")
    run("-ss", "5", "-i", str(movie), "-frames:v", "1", str(poster))
    records.append({"locale": locale, "frames": count, "seconds": seconds, "gifSeconds": duration / 1000, "sources": [path.name for path in inputs], "comparison": movie.name})
(OUTPUT / "comparison-encoding-v6.json").write_text(json.dumps(records, indent=2), encoding="utf-8")
print(json.dumps(records, indent=2))


