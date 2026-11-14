"""Compose the #1339 contact sheets from FactionDebrisSheet frames.

usage: sheet.py <frames_dir> <kill_index> <out_prefix> [--times 0.12,0.3,0.6] [--fps-dt <seconds per frame>]

Frames are 1920x1080 at an orthographic half-height of 15 world units centred on the origin,
so one world unit is 36 px. Each pair sits at (column, row) with the intact twin on the left.
"""
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFont

COLUMNS = [-19.5, -6.5, 6.5, 19.5]
ROWS = [10.6, 3.5, -3.5, -10.6]
PX = 36.0
CELLS = [
    ("Shoal", ["Crimson", "Valis", "Valis elite", None]),
    ("Veil", ["Crimson", "Nightshade", "Nightshade elite", None]),
    ("Bastion", ["Crimson", "Bulwark", "Bulwark elite", None]),
    ("Player paint", ["Crimson", "Valis", "Nightshade", "Bulwark"]),
]


def font(size):
    for name in ("segoeui.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def to_px(x, y):
    return 960 + PX * x, 540 - PX * y


def crop_pair(frame, column, row, half_w=6.4, half_h=3.55):
    cx, cy = to_px(COLUMNS[column], ROWS[row])
    box = (int(cx - half_w * PX), int(cy - half_h * PX), int(cx + half_w * PX), int(cy + half_h * PX))
    return frame.crop(box)


def labelled_frame(frame, title):
    out = frame.convert("RGB").copy()
    draw = ImageDraw.Draw(out)
    small = font(20)
    big = font(26)
    draw.text((16, 10), title, fill=(235, 235, 235), font=big)
    for r, (faction, ships) in enumerate(CELLS):
        for c, ship in enumerate(ships):
            if ship is None:
                continue
            cx, cy = to_px(COLUMNS[c], ROWS[r])
            text = f"{faction}: {ship}" if r < 3 else f"Player: {ship}"
            width = draw.textlength(text, font=small)
            draw.text((cx - width / 2, cy - 3.45 * PX), text, fill=(200, 205, 215), font=small)
    return out


def zoom_sheet(frames, titles, rows, scale=2):
    """Rows = the chosen pairs, columns = the chosen moments, each crop scaled up."""
    pairs = [(r, c, f"{CELLS[r][0]}: {ship}" if r < 3 else f"Player: {ship}")
             for r in rows for c, ship in enumerate(CELLS[r][1]) if ship is not None]
    sample = crop_pair(frames[0], 0, 0)
    cw, ch = sample.width * scale, sample.height * scale
    label_w, head_h, gap = 250, 44, 6
    sheet = Image.new("RGB", (label_w + len(frames) * (cw + gap), head_h + len(pairs) * (ch + gap)), (12, 13, 18))
    draw = ImageDraw.Draw(sheet)
    head = font(24)
    side = font(22)
    for i, title in enumerate(titles):
        draw.text((label_w + i * (cw + gap) + 10, 8), title, fill=(235, 235, 235), font=head)
    for j, (r, c, name) in enumerate(pairs):
        y = head_h + j * (ch + gap)
        draw.text((10, y + ch / 2 - 12), name, fill=(215, 220, 230), font=side)
        for i, frame in enumerate(frames):
            crop = crop_pair(frame, c, r).resize((cw, ch), Image.LANCZOS)
            sheet.paste(crop, (label_w + i * (cw + gap), y))
    return sheet


def main():
    frames_dir, kill_index, prefix = sys.argv[1], int(sys.argv[2]), sys.argv[3]
    offsets = [int(v) for v in sys.argv[4].split(",")] if len(sys.argv) > 4 else [-5, 6, 15, 30]
    paths = sorted(glob.glob(os.path.join(frames_dir, "*.png")))
    picked = [Image.open(paths[kill_index + off]).convert("RGB") for off in offsets]
    titles = ["before the kill" if off < 0 else f"{off * 0.02:.2f} s after" for off in offsets]
    for image, off in zip(picked, offsets):
        name = "intact" if off < 0 else f"plus{off:02d}"
        labelled_frame(image, f"Game distance, {'before the kill' if off < 0 else f'{off * 0.02:.2f} s after the kill'}"
                       ).save(f"{prefix}-game-{name}.png")
    zoom_sheet(picked, titles, [0, 1, 2], scale=1).save(f"{prefix}-factions.png")
    zoom_sheet(picked, titles, [3], scale=1).save(f"{prefix}-player.png")
    print("wrote", prefix)


if __name__ == "__main__":
    main()
