"""Colour the pass renders from colour.json and lay them over real locale captures.

Run: python compose.py <passes dir> <backdrops dir> <sheets dir> [<earlier passes dir>]
The tuner page does the same sums in the browser; colour.json is the one settings block for both.
"""
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

PASSES, BACKDROPS, DST = (Path(a) for a in sys.argv[1:4])
EARLIER = Path(sys.argv[4]) if len(sys.argv) > 4 else None  # passes from the gate before, for a before and after
PREFIX = "detail" if EARLIER else "colour"
SETTINGS = json.loads((Path(__file__).parent / "colour.json").read_text(encoding="utf-8"))
GROUPS = {"A": ("bone", "canvas", "slate"), "B": ("ochre", "brick", "blue")}
NEAR, FAR = 24.0, 14.4  # px per unit on a 720 px tall screen at the camera's closest and widest zoom
RENDER = 24.0           # the passes are rendered at the closest zoom
BRUSH_CONTRAST = 3.2    # the contrast the ships' paint uses for the same tile
STATIONS = {"gimbal": "REPAIR", "kite": "MOD STATION", "drum": "ARMORY"}
LOCALES = {"copper": "COPPER TURQUOISE", "chalk": "ILLUSTRATED BLUE", "verdant": "VERDANT GIANT", "ice": "BLUE CRESCENT"}
INK_TEXT, DIM_TEXT, NAVY = (232, 228, 216), (150, 156, 182), (10, 12, 28)


def to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def hex_linear(h):
    h = h.lstrip("#")
    return to_linear(np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32))


def coverage(station, view, name):
    """Per-channel coverage of one pass: the stored colour is straight, so weight it by alpha."""
    im = np.asarray(Image.open(PASSES / f"{station}-{view}-{name}.png").convert("RGBA")).astype(np.float32) / 255
    return to_linear(im[..., :3]) * im[..., 3:], im[..., 3]


def colour_station(station, view, state, s=SETTINGS):
    """Premultiplied linear colour, alpha and the light that should bloom."""
    hull, hull_cov, mid_all, shade_all, alpha = 0.0, 0.0, 0.0, 0.0, None
    tint = np.array([s["shadow_depth"], s["shadow_depth"], s["shadow_depth"] + s["shadow_cool"]], np.float32)
    for group, keys in GROUPS.items():
        cov, alpha = coverage(station, view, "cov" + group)
        lit, _ = coverage(station, view, "lit" + group)
        mid, _ = coverage(station, view, "mid" + group)
        for i, key in enumerate(keys):
            base = hex_linear(s["hull"][key])
            shadow = base * tint
            middle = shadow + (base - shadow) * s["mid_level"]
            shade = np.clip(cov[..., i] - lit[..., i] - mid[..., i], 0, 1)
            hull = hull + lit[..., i, None] * base + mid[..., i, None] * middle + shade[..., None] * shadow
            hull_cov, mid_all, shade_all = hull_cov + cov[..., i], mid_all + mid[..., i], shade_all + shade
    ink_colour = hex_linear(s["ink"])
    safe = np.maximum(hull_cov, 1e-4)
    if (PASSES / f"{station}-{view}-brush.png").exists():
        # The ships' brush tile lifts and drops the hull colour in broad painted patches.
        brush = coverage(station, view, "brush")[0][..., 0] / safe
        hull = hull * (1 + s["brush"] * BRUSH_CONTRAST * (brush - 0.625))[..., None]
        # Hatching: one set of strokes in the half-tone and shadow, a crossing set in shadow only.
        hatch = coverage(station, view, "hatch")[0] / safe[..., None]
        inked = (hatch[..., 0] * (mid_all + shade_all) + hatch[..., 1] * shade_all) / safe
        inked = np.clip(s["hatch"] * inked, 0, 1)[..., None]
        hull = hull * (1 - inked) + ink_colour * inked * hull_cov[..., None]
    out, total = hull, hull_cov
    emis, _ = coverage(station, view, "emis")
    kind = station.split("-")[0]
    lights = {"win": hex_linear(s["window"]), "svc": hex_linear(s["service"][kind]), "core": hex_linear(s["prize"])}
    if state == "dark":
        lights = {k: hex_linear(s["dark"]) for k in lights}
    glow = 0.0
    for i, key in enumerate(("win", "svc", "core")):
        out = out + emis[..., i, None] * lights[key]
        total = total + emis[..., i]
        if state != "dark":
            flare = s["in_use_boost"] if (state == "in use" and key == "svc") else 1.0
            glow = glow + emis[..., i, None] * lights[key] * flare
    ink = np.clip(alpha - total, 0, 1)
    out = out + ink[..., None] * ink_colour
    if isinstance(glow, float):
        glow = np.zeros_like(out)
    return out.astype(np.float32), alpha.astype(np.float32), (glow * s["glow"]).astype(np.float32)


def place(frame, station, view, state, scale, centre):
    """Draw a station into a linear RGB frame at px-per-unit `scale`, centred on `centre`."""
    rgb, alpha, glow = colour_station(station, view, state)
    size = round(rgb.shape[0] * scale / RENDER)
    rgb, alpha, glow = (cv2.resize(a, (size, size), interpolation=cv2.INTER_AREA) for a in (rgb, alpha, glow))
    k = scale / RENDER
    bloom = cv2.GaussianBlur(glow, (0, 0), 9 * k) * 0.9 + cv2.GaussianBlur(glow, (0, 0), 30 * k) * 0.6
    x0, y0 = round(centre[0] - size / 2), round(centre[1] - size / 2)
    h, w = frame.shape[:2]
    fx0, fy0, fx1, fy1 = max(x0, 0), max(y0, 0), min(x0 + size, w), min(y0 + size, h)
    sx0, sy0 = fx0 - x0, fy0 - y0
    sub = (slice(sy0, sy0 + fy1 - fy0), slice(sx0, sx0 + fx1 - fx0))
    region = frame[fy0:fy1, fx0:fx1]
    region = region * (1 - alpha[sub][..., None]) + rgb[sub]
    frame[fy0:fy1, fx0:fx1] = 1 - (1 - np.clip(region, 0, 1)) * (1 - np.clip(bloom[sub], 0, 1))


def backdrop(name, scale):
    """A 1280x720 game capture at the closest zoom, rescaled to px-per-unit `scale`, in linear RGB."""
    if name == "navy":
        return np.tile(to_linear(np.array(NAVY, np.float32) / 255), (720, 1280, 1)).astype(np.float32)
    im = Image.open(BACKDROPS / f"{name}-flight.png").convert("RGB")
    if scale != NEAR:
        im = im.resize((round(im.width * scale / NEAR), round(im.height * scale / NEAR)), Image.LANCZOS)
    return to_linear(np.asarray(im).astype(np.float32) / 255).astype(np.float32)


def image(frame):
    return Image.fromarray((to_srgb(frame) * 255 + 0.5).astype(np.uint8))


def font(size, bold=False):
    try:
        f = ImageFont.truetype("C:/Windows/Fonts/bahnschrift.ttf", size)
        f.set_variation_by_name("SemiBold Condensed" if bold else "Light Condensed")
        return f
    except Exception:
        return ImageFont.load_default()


def tag(draw, xy, text, size=18, fill=INK_TEXT, bold=False):
    x, y = xy
    draw.text((x + 1, y + 1), text, font=font(size, bold), fill=(0, 0, 0))
    draw.text((x, y), text, font=font(size, bold), fill=fill)


def screen_frame(station, locale, view="tumble1"):
    """What a 1280x720 screen shows with the ship parked at the station."""
    frame = backdrop(locale, NEAR)
    place(frame, station, view, "open", NEAR, (770, 330))
    out = image(frame)
    d = ImageDraw.Draw(out)
    tag(d, (16, 12), f"{STATIONS[station]} ON {LOCALES[locale]}", 24, bold=True)
    tag(d, (16, 44), "A full 1280x720 screen at the camera's closest zoom. The ship is the one in the capture.", 17, DIM_TEXT)
    out.save(DST / f"{PREFIX}-screen-{station}-{locale}.png")


def window_compare(locale="copper", view="tumble1"):
    """The same repair dock at the closest zoom with large and with small windows."""
    out = Image.new("RGB", (1280, 1440))
    d = ImageDraw.Draw(out)
    for row, (station, text) in enumerate((("gimbal", "WINDOWS AS IN THE BLOCKOUT: ONE PANE IS HALF A SHIP WIDE"),
                                           ("gimbal-smallwin", "WINDOWS AT HALF THE SIZE, IN TWO ROWS"))):
        frame = backdrop(locale, NEAR)
        place(frame, station, view, "open", NEAR, (770, 330))
        out.paste(image(frame), (0, row * 720))
        tag(d, (16, row * 720 + 12), text, 24, bold=True)
        tag(d, (16, row * 720 + 44), "A full 1280x720 screen at the camera's closest zoom.", 17, DIM_TEXT)
    out.save(DST / "colour-window-size-compare.png")


def before_after(station="gimbal", earlier="gimbal-smallwin", locale="copper", view="tumble1"):
    """One station at the closest zoom: as the colour gate left it, and after the detail pass."""
    global PASSES
    out = Image.new("RGB", (1280, 1440))
    d = ImageDraw.Draw(out)
    current = PASSES
    for row, (folder, name, text) in enumerate(((EARLIER, earlier, "BEFORE: COLOUR AND SMALL WINDOWS ONLY"),
                                                (current, station, "AFTER: HARDWARE, INK MARKS, HATCHING AND BRUSH TEXTURE"))):
        PASSES = folder
        frame = backdrop(locale, NEAR)
        place(frame, name, view, "open", NEAR, (770, 330))
        out.paste(image(frame), (0, row * 720))
        tag(d, (16, row * 720 + 12), text, 24, bold=True)
        tag(d, (16, row * 720 + 44), "A full 1280x720 screen at the camera's closest zoom.", 17, DIM_TEXT)
    PASSES = current
    out.save(DST / f"{PREFIX}-before-after.png")


def backdrop_grid(view="tumble1"):
    """Every station on every locale, at the camera's widest zoom."""
    cell_w, cell_h = 640, 432
    locales = list(LOCALES)
    head = 84
    sheet = Image.new("RGB", (cell_w * len(locales), head + cell_h * len(STATIONS)), NAVY)
    d = ImageDraw.Draw(sheet)
    tag(d, (20, 14), "EVERY STATION ON EVERY LOCALE", 32, bold=True)
    tag(d, (20, 52), "True size at the camera's widest zoom on a 720 px tall screen. Backdrops are game captures, "
                     "asteroids and ship included.", 18, DIM_TEXT)
    for row, station in enumerate(STATIONS):
        for col, locale in enumerate(locales):
            full = backdrop(locale, FAR)
            cy, cx = full.shape[0] // 2, full.shape[1] // 2
            frame = np.ascontiguousarray(full[cy - cell_h // 2:cy + cell_h // 2, cx - cell_w // 2:cx + cell_w // 2 + 1])
            place(frame, station, view, "open", FAR, (cell_w / 2 + 50, cell_h / 2 - 12))
            x, y = col * cell_w, head + row * cell_h
            sheet.paste(image(frame), (x, y))
            tag(d, (x + 10, y + 8), f"{STATIONS[station]}  /  {LOCALES[locale]}", 16)
    sheet.save(DST / f"{PREFIX}-backdrops-sheet.png")


def states_sheet(locale="chalk", view="tumble1"):
    """Open, in use and dark for each station."""
    cell_w, cell_h = 640, 432
    states = ("open", "in use", "dark")
    head = 84
    sheet = Image.new("RGB", (cell_w * 3, head + cell_h * 3), NAVY)
    d = ImageDraw.Draw(sheet)
    tag(d, (20, 14), "THREE STATES", 32, bold=True)
    tag(d, (20, 52), "Open: lights on. In use: the service lights flare. Dark: every light out, hull and outline stay.",
        18, DIM_TEXT)
    full = backdrop(locale, FAR)
    cy, cx = full.shape[0] // 2, full.shape[1] // 2
    for row, station in enumerate(STATIONS):
        for col, state in enumerate(states):
            frame = np.ascontiguousarray(full[cy - cell_h // 2:cy + cell_h // 2, cx - cell_w // 2:cx + cell_w // 2 + 1])
            place(frame, station, view, state, FAR, (cell_w / 2 + 50, cell_h / 2 - 12))
            x, y = col * cell_w, head + row * cell_h
            sheet.paste(image(frame), (x, y))
            tag(d, (x + 10, y + 8), f"{STATIONS[station]}  /  {state.upper()}", 16)
    sheet.save(DST / f"{PREFIX}-states-sheet.png")


if __name__ == "__main__":
    DST.mkdir(parents=True, exist_ok=True)
    backdrop_grid()
    states_sheet()
    if EARLIER:
        before_after()
    else:
        window_compare()
    screen_frame("gimbal", "copper")
    screen_frame("kite", "ice")
    screen_frame("drum", "verdant")
    print("composed")
