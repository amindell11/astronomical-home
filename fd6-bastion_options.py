"""Bastion palette options beside neutral and round 4, every station at the widest zoom.

Run: python bastion_options.py <passes dir> <backdrops dir> <sheets dir> [locale]
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
LOCALE = sys.argv[4] if len(sys.argv) > 4 else "copper"
sys.argv = sys.argv[:4]
import compose_dress as cd  # noqa: E402

FRAME = "#241F22"
OPTIONS = {
    "ROUND 4": {},
    "A: BONE, RED ACCENTS": {"bone": "#EBCFA4", "canvas": "#BFA57C", "slate": FRAME,
                             "ochre": "#8A2A38", "brick": "#EBCB9E", "blue": "#561A26"},
    "C: BONE, MORE RED ACCENTS": {"bone": "#EBCFA4", "canvas": "#BFA57C", "slate": FRAME,
                                  "ochre": "#8A2A38", "brick": "#6A1E2A", "blue": "#561A26"},
}


def option(hull):
    s = cd.settings("bastion")
    s["hull"].update(hull)
    return s


cell_w, cell_h, head = 600, 432, 84
cols = [("NEUTRAL", None)] + list(OPTIONS.items())
sheet = cd.Image.new("RGB", (cell_w * len(cols), head + cell_h * len(cd.STATIONS)), cd.NAVY)
d = cd.ImageDraw.Draw(sheet)
cd.tag(d, (20, 14), f"BASTION: HOW MUCH RED? {cd.LOCALES[LOCALE]}", 32, bold=True)
cd.tag(d, (20, 52), "True size at the camera's widest zoom. A keeps red to the armour, banners and chevrons. C also paints "
                    "the brick panels and stripes deep red.", 18, cd.DIM_TEXT)
full = cd.backdrop(LOCALE, cd.FAR)
for row, station in enumerate(cd.STATIONS):
    for col, (label, hull) in enumerate(cols):
        frame = cd.crop(full, cell_w, cell_h)
        if hull is None:
            cd.place(frame, station, "tumble1", "open", cd.FAR, (cell_w / 2, cell_h / 2), cd.settings("neutral"))
        else:
            cd.place(frame, cd.tag_of(station, "bastion"), "tumble1", "open", cd.FAR, (cell_w / 2, cell_h / 2), option(hull))
        x, y = col * cell_w, head + row * cell_h
        sheet.paste(cd.image(frame), (x, y))
        cd.tag(d, (x + 10, y + 8), f"{cd.STATIONS[station]}  /  {label}", 16)
sheet.save(cd.DST / f"fd6-bastion-options-{LOCALE}.png")
print("composed")
