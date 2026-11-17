"""Faction dress sheets for #1592: the pass renders recoloured per faction and laid over locale captures.

Run: python compose_dress.py <passes dir> <backdrops dir> <sheets dir>
dress-colour.json is the one settings block: the accepted look plus each faction's accents and window light.
"""
import copy
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

PASSES, BACKDROPS, DST = (Path(a) for a in sys.argv[1:4])
BASE = json.loads((Path(__file__).parent / "dress-colour.json").read_text(encoding="utf-8"))
GROUPS = {"A": ("bone", "canvas", "slate"), "B": ("ochre", "brick", "blue")}
NEAR, FAR, RENDER = 24.0, 14.4, 24.0
BRUSH_CONTRAST = 3.2
STATIONS = {"gimbal": "REPAIR", "kite": "MOD STATION", "drum": "ARMORY"}
FACTIONS = {"neutral": "NEUTRAL", "shoal": "SHOAL SPACE", "veil": "VEIL SPACE", "bastion": "BASTION SPACE"}
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


def settings(faction, service="accepted"):
    s = copy.deepcopy(BASE)
    if faction != "neutral":
        s["hull"].update(BASE["factions"][faction]["hull"])
        s["window"] = BASE["factions"][faction]["window"]
    if service == "minimap":
        s["service"] = dict(BASE["minimap"])
    return s


def tag_of(station, faction):
    return station if faction == "neutral" else f"{station}-{faction}-kit"


def coverage(name, view, kind):
    im = np.asarray(Image.open(PASSES / f"{name}-{view}-{kind}.png").convert("RGBA")).astype(np.float32) / 255
    return to_linear(im[..., :3]) * im[..., 3:], im[..., 3]


def colour_station(name, view, state, s):
    """Premultiplied linear colour, alpha and the light that should bloom, as the colour gate's compositor does."""
    hull, hull_cov, mid_all, shade_all, alpha = 0.0, 0.0, 0.0, 0.0, None
    tint = np.array([s["shadow_depth"], s["shadow_depth"], s["shadow_depth"] + s["shadow_cool"]], np.float32)
    for group, keys in GROUPS.items():
        cov, alpha = coverage(name, view, "cov" + group)
        lit, _ = coverage(name, view, "lit" + group)
        mid, _ = coverage(name, view, "mid" + group)
        for i, key in enumerate(keys):
            base = hex_linear(s["hull"][key])
            shadow = base * tint
            middle = shadow + (base - shadow) * s["mid_level"]
            shade = np.clip(cov[..., i] - lit[..., i] - mid[..., i], 0, 1)
            hull = hull + lit[..., i, None] * base + mid[..., i, None] * middle + shade[..., None] * shadow
            hull_cov, mid_all, shade_all = hull_cov + cov[..., i], mid_all + mid[..., i], shade_all + shade
    ink_colour = hex_linear(s["ink"])
    safe = np.maximum(hull_cov, 1e-4)
    brush = coverage(name, view, "brush")[0][..., 0] / safe
    hull = hull * (1 + s["brush"] * BRUSH_CONTRAST * (brush - 0.625))[..., None]
    hatch = coverage(name, view, "hatch")[0] / safe[..., None]
    inked = (hatch[..., 0] * (mid_all + shade_all) + hatch[..., 1] * shade_all) / safe
    inked = np.clip(s["hatch"] * inked, 0, 1)[..., None]
    hull = hull * (1 - inked) + ink_colour * inked * hull_cov[..., None]
    out, total = hull, hull_cov
    emis, _ = coverage(name, view, "emis")
    kind = name.split("-")[0]
    lights = {"win": hex_linear(s["window"]), "svc": hex_linear(s["service"][kind]), "core": hex_linear(s["prize"])}
    if state == "dark":
        lights = {k: hex_linear(s["dark"]) for k in lights}
    glow = np.zeros_like(out)
    for i, key in enumerate(("win", "svc", "core")):
        out = out + emis[..., i, None] * lights[key]
        total = total + emis[..., i]
        if state != "dark":
            glow = glow + emis[..., i, None] * lights[key]
    ink = np.clip(alpha - total, 0, 1)
    out = out + ink[..., None] * ink_colour
    return out.astype(np.float32), alpha.astype(np.float32), (glow * s["glow"]).astype(np.float32)


def place(frame, name, view, state, scale, centre, s):
    rgb, alpha, glow = colour_station(name, view, state, s)
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


def crop(full, w, h):
    cy, cx = full.shape[0] // 2, full.shape[1] // 2
    return np.ascontiguousarray(full[cy - h // 2:cy + h // 2, cx - w // 2:cx - w // 2 + w])


def faction_grid(locale, state="open", view="tumble1"):
    """Every station in every faction's dress on one locale, at the camera's widest zoom."""
    cell_w, cell_h, head = 600, 432, 84
    sheet = Image.new("RGB", (cell_w * len(FACTIONS), head + cell_h * len(STATIONS)), NAVY)
    d = ImageDraw.Draw(sheet)
    title = "FACTION DRESS" + ("" if state == "open" else f", {state.upper()}")
    tag(d, (20, 14), f"{title} ON {LOCALES[locale]}", 32, bold=True)
    tag(d, (20, 52), "True size at the camera's widest zoom on a 720 px tall screen. Each faction's kit and accents on the "
                     "same hulls. Service lights unchanged.", 18, DIM_TEXT)
    full = backdrop(locale, FAR)
    for row, station in enumerate(STATIONS):
        for col, faction in enumerate(FACTIONS):
            frame = crop(full, cell_w, cell_h)
            place(frame, tag_of(station, faction), view, state, FAR, (cell_w / 2, cell_h / 2), settings(faction))
            x, y = col * cell_w, head + row * cell_h
            sheet.paste(image(frame), (x, y))
            tag(d, (x + 10, y + 8), f"{STATIONS[station]}  /  {FACTIONS[faction]}", 16)
    sheet.save(DST / f"fd1-factions-{locale}{'' if state == 'open' else '-' + state}.png")


def close_sheet(station, locale, view="top"):
    """One station face on at the camera's closest zoom, in each dress, to judge the parts themselves."""
    cell, head = 720, 84
    sheet = Image.new("RGB", (cell * 2, head + cell * 2), NAVY)
    d = ImageDraw.Draw(sheet)
    tag(d, (20, 14), f"{STATIONS[station]} UP CLOSE ON {LOCALES[locale]}", 32, bold=True)
    tag(d, (20, 52), "The camera's closest zoom, station face on. Neutral is the accepted station.", 18, DIM_TEXT)
    full = backdrop(locale, NEAR)
    for i, faction in enumerate(FACTIONS):
        frame = crop(full, cell, cell)
        place(frame, tag_of(station, faction), view, "open", NEAR, (cell / 2, cell / 2), settings(faction))
        x, y = (i % 2) * cell, head + (i // 2) * cell
        sheet.paste(image(frame), (x, y))
        tag(d, (x + 12, y + 10), FACTIONS[faction], 20, bold=True)
    sheet.save(DST / f"fd1-close-{station}-{locale}.png")


def service_sheet(locale="chalk", view="tumble1"):
    """The service lights as accepted, and in the colours the minimap gives each kind."""
    cell_w, cell_h, head = 600, 432, 84
    rows = (("accepted", "AS ACCEPTED ON #1384"), ("minimap", "IN THE MINIMAP'S FIND COLOURS"))
    sheet = Image.new("RGB", (cell_w * 3, head + cell_h * 2), NAVY)
    d = ImageDraw.Draw(sheet)
    tag(d, (20, 14), "SERVICE LIGHTS: WORLD AGAINST MINIMAP", 32, bold=True)
    tag(d, (20, 52), "Top: the lights as tuned (repair 5BFF86, mod FFE25A, armory FF3B52). Bottom: the minimap's "
                     "find colours (5FE0C0, FFD27A, 8C9CFF).", 18, DIM_TEXT)
    full = backdrop(locale, FAR)
    for row, (service, text) in enumerate(rows):
        for col, station in enumerate(STATIONS):
            frame = crop(full, cell_w, cell_h)
            place(frame, station, view, "open", FAR, (cell_w / 2, cell_h / 2), settings("neutral", service))
            x, y = col * cell_w, head + row * cell_h
            sheet.paste(image(frame), (x, y))
            tag(d, (x + 10, y + 8), f"{STATIONS[station]}  /  {text}", 16)
    sheet.save(DST / "fd1-service-colours.png")


def veil_windows(locale="copper", view="tumble1"):
    """Veil space with three window lights, beside the repair green and the mod gold they must not echo."""
    cell_w, cell_h, head = 600, 432, 84
    options = BASE["veil_windows"]
    sheet = Image.new("RGB", (cell_w * len(options), head + cell_h * 2), NAVY)
    d = ImageDraw.Draw(sheet)
    tag(d, (20, 14), "VEIL SPACE: WHICH WINDOW LIGHT?", 32, bold=True)
    tag(d, (20, 52), "The Veil's acid green sits close to the repair lights. Violet and ice keep clear of every service "
                     "colour.", 18, DIM_TEXT)
    full = backdrop(locale, FAR)
    for row, station in enumerate(("gimbal", "kite")):
        for col, (label, colour) in enumerate(options.items()):
            s = settings("veil")
            s["window"] = colour
            frame = crop(full, cell_w, cell_h)
            place(frame, tag_of(station, "veil"), view, "open", FAR, (cell_w / 2, cell_h / 2), s)
            x, y = col * cell_w, head + row * cell_h
            sheet.paste(image(frame), (x, y))
            tag(d, (x + 10, y + 8), f"{STATIONS[station]}  /  {label.upper()} {colour}", 16)
    sheet.save(DST / "fd1-veil-windows.png")


if __name__ == "__main__":
    DST.mkdir(parents=True, exist_ok=True)
    veil_windows()
    for locale in ("copper", "verdant"):
        faction_grid(locale)
    faction_grid("copper", "dark")
    close_sheet("gimbal", "chalk")
    close_sheet("drum", "chalk")
    close_sheet("kite", "chalk")
    service_sheet()
    print("composed")
