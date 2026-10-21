"""Composite #1059 review clips: the preview render placed in the real 1920x1080 hangar layout.

usage: python compose.py <frames-root> <baseline.mp4> <out-dir>
"""
import glob, os, subprocess, sys
import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont

FF = imageio_ffmpeg.get_ffmpeg_exe()
W, H = 1280, 720
S = W / 1920
PANEL_W = round(660 * S)
OLD = (round(690 * S), round((1080 - 1170) * S), round(1890 * S), round((1080 + 30) * S))  # old preview rect
BG = (9, 13, 23)
FONT = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 22)
SMALL = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 18)


def layout(img):
    """Darken the menu panel strip as the 55% panel does, outline the old preview rect."""
    img = img.convert("RGB")
    over = Image.new("RGB", (PANEL_W, H), (5, 10, 23))
    img.paste(Image.blend(img.crop((0, 0, PANEL_W, H)), over, .55), (0, 0))
    d = ImageDraw.Draw(img)
    for x in range(OLD[0], OLD[2], 14):
        d.line([(x, 1), (x + 6, 1)], fill=(70, 80, 100))
        d.line([(x, H - 2), (x + 6, H - 2)], fill=(70, 80, 100))
    for y in range(0, H, 14):
        d.line([(OLD[0], y), (OLD[0], y + 6)], fill=(70, 80, 100))
    d.text((12, 8), "menu panel (55%)", font=SMALL, fill=(110, 120, 140))
    return img


def caption(img, text, sub=""):
    bar = Image.new("RGB", (W, 40), (20, 24, 34))
    d = ImageDraw.Draw(bar)
    d.text((12, 6), text, font=FONT, fill=(230, 232, 240))
    if sub:
        d.text((W - 12 - d.textlength(sub, font=FONT), 6), sub, font=FONT, fill=(255, 200, 110))
    out = Image.new("RGB", (W, H + 40))
    out.paste(bar, (0, 0))
    out.paste(img, (0, 40))
    return out


def baseline_frames(mp4, tmp, count):
    os.makedirs(tmp, exist_ok=True)
    subprocess.run([FF, "-v", "error", "-y", "-i", mp4, "-frames:v", str(count),
                    "-vf", "crop=640:640:0:80", os.path.join(tmp, "b-%04d.png")], check=True)
    return sorted(glob.glob(os.path.join(tmp, "b-*.png")))


def place_baseline(path):
    canvas = Image.new("RGB", (W, H), BG)
    sq = Image.open(path).convert("RGB").resize((OLD[2] - OLD[0], OLD[3] - OLD[1]), Image.LANCZOS)
    canvas.paste(sq, (OLD[0], OLD[1]))
    return canvas


def presses(frames_dir):
    rows = [l.split("\t") for l in open(os.path.join(frames_dir, "presses.tsv"), encoding="utf-8").read().splitlines()]
    return {int(f): (int(s), name) for f, s, name in rows}


def press_text(log, f):
    recent = [log[g][0] for g in range(max(0, f - 4), f + 1) if g in log and log[g][0]]
    arrow = ("Next >" if recent[-1] > 0 else "< Previous") if recent else ""
    return arrow, log[f][1] if f in log else ""


def encode(frame_dir, out, fps=30):
    subprocess.run([FF, "-v", "error", "-y", "-framerate", str(fps), "-i", os.path.join(frame_dir, "c-%04d.png"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", out], check=True)
    subprocess.run([FF, "-v", "error", "-y", "-i", out, "-vf",
                    "fps=15,scale=640:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=96[p];[b][p]paletteuse",
                    out[:-4] + ".gif"], check=True)


def compose_single(root, mp4, out, tmp, single):
    log = presses(os.path.join(root, "single-frames"))
    base = baseline_frames(mp4, os.path.join(tmp, "base"), len(single))
    sdir = os.path.join(tmp, "single"); os.makedirs(sdir, exist_ok=True)
    for i, (b, n) in enumerate(zip(base, single)):
        arrow, ship = press_text(log, i)
        top = caption(layout(place_baseline(b)), "main (#1024): preview is the 1200 square", arrow)
        bottom = caption(layout(Image.open(n)), f"#1059 branch: preview spans the screen   [{ship}]", arrow)
        frame = Image.new("RGB", (W, 2 * (H + 40)))
        frame.paste(top, (0, 0)); frame.paste(bottom, (0, H + 40))
        frame.resize((W * 3 // 4, (H + 40) * 3 // 2), Image.LANCZOS).save(os.path.join(sdir, f"c-{i:04d}.png"))
    encode(sdir, os.path.join(out, "single-press-side-by-side.mp4"))



def main(root, mp4, out):
    os.makedirs(out, exist_ok=True)
    tmp = os.path.join(out, "tmp")

    single = sorted(glob.glob(os.path.join(root, "single-frames", "frame-*.png")))
    if single:
        compose_single(root, mp4, out, tmp, single)

    spam = sorted(glob.glob(os.path.join(root, "spam-frames", "frame-*.png")))
    log = presses(os.path.join(root, "spam-frames"))
    phases = [(60, 150, "alternating, every 0.3 s"), (210, 300, "alternating, 10 Hz"),
              (360, 450, "Next, 10 Hz (wraps)"), (510, 600, "Previous, 10 Hz (wraps)")]
    pdir = os.path.join(tmp, "spam"); os.makedirs(pdir, exist_ok=True)
    for i, n in enumerate(spam):
        arrow, ship = press_text(log, i)
        phase = next((p for a, b, p in phases if a <= i < b + 15), "settle")
        caption(layout(Image.open(n)), f"#1059 {phase}   [{ship}]   t={i / 30:4.1f}s", arrow).save(
            os.path.join(pdir, f"c-{i:04d}.png"))
    encode(pdir, os.path.join(out, "bursts.mp4"))


if __name__ == "__main__":
    main(*sys.argv[1:4])
