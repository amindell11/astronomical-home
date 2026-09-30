"""Generates the laser-bolt concept layers as SVGs (one file per layer, per colorway)."""
import math
import random
from pathlib import Path

OUT = Path(__file__).parent

W, H = 2048, 256
CY = H / 2
TAIL, NOSE = 40, 2000
FRAMES = 4

PALETTES = {
    "blue": dict(core="#FFFFFF", hot="#D9F7FF", mid="#5CCBFF", rim="#2D6BFF", fringe="#8A7BFF", glow="#3F8CFF"),
    "red": dict(core="#FFFFFF", hot="#FFF1C9", mid="#FF8A3D", rim="#F0331E", fringe="#FF4FA0", glow="#FF4A2A"),
}

# Band half-heights at the widest point, and the zigzag amplitude each band's edge carries.
BANDS = [("rim", 30, 11), ("mid", 23, 6), ("hot", 17, 2.5), ("core", 11, 0.9)]


def profile(t):
    """Half-height fraction along the bolt, t=0 at tail tip, t=1 at nose."""
    peak = 0.86
    if t < peak:
        return (t / peak) ** 0.55
    u = (t - peak) / (1 - peak)
    return math.sqrt(max(0.0, 1 - u * u))


def svg(body, w=W, h=H, defs=""):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}">'
            f"<defs>{defs}</defs>{body}</svg>")


def jag_edge(rng, half, amp, sign, x0, x1):
    """Irregular zigzag along one side of a band: alternating out/in kinks, occasional big spike."""
    pts = []
    x = x0
    out = True
    phase = rng.uniform(0, 6.28)
    while x < x1:
        t = (x - TAIL) / (NOSE - TAIL)
        base = half * profile(t)
        # Low-frequency envelope: calm stretches broken by crackling bursts.
        env = 0.25 + 0.75 * max(0.0, math.sin(x * 0.011 + phase) * math.sin(x * 0.0043 + phase * 2.3)) ** 0.5
        a = amp * env * min(1.0, profile(t) * 1.6)
        if out:
            off = rng.uniform(0.3, 1.0) * a * (2.4 if rng.random() < 0.1 else 1.0)
        else:
            off = -rng.uniform(0.1, 0.5) * a
        pts.append((x, CY + sign * max(0.0, base + off)))
        x += rng.uniform(8, 46) * (0.6 if t > 0.8 else 1.0)
        out = not out
    return pts


def band_path(rng, half, amp, shrink):
    x0, x1 = TAIL + shrink * 6, NOSE - shrink * 1.2
    top = jag_edge(rng, half, amp, -1, x0, x1)
    bot = jag_edge(rng, half, amp, 1, x0, x1)
    pts = [(x0, CY)] + top + [(x1, CY)] + bot[::-1]
    return "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in pts) + "Z"


def body_layer(pal, seed):
    rng = random.Random(seed)
    parts = []
    for i, (name, half, amp) in enumerate(BANDS):
        shrink = 30 - half
        parts.append(f'<path d="{band_path(rng, half, amp, shrink)}" fill="{pal[name]}"/>')
    # Anime specular slash on the head, the one hard highlight.
    parts.append(f'<path d="M1930,{CY-3} L1760,{CY-5} L1560,{CY-3.2} L1760,{CY-1.6}Z" fill="#FFFFFF"/>')
    return svg("".join(parts))


def lightning(rng, x, y, length, direction, spread):
    pts = [(x, y)]
    for _ in range(int(length / 14)):
        x += direction * rng.uniform(8, 20)
        y += rng.uniform(-spread, spread)
        pts.append((x, y))
    return pts


def fringe_layer(pal, seed):
    rng = random.Random(seed * 7 + 3)
    parts = []
    for _ in range(9):
        t = rng.uniform(0.25, 0.92)
        x = TAIL + t * (NOSE - TAIL)
        side = rng.choice((-1, 1))
        y = CY + side * (30 * profile(t) + rng.uniform(-2, 6))
        pts = lightning(rng, x, y, rng.uniform(80, 260), rng.choice((-1, 1)), 7)
        w = rng.uniform(1.6, 3.2)
        d = "M" + " L".join(f"{a:.1f},{b:.1f}" for a, b in pts)
        parts.append(f'<path d="{d}" fill="none" stroke="{pal["fringe"]}" stroke-width="{w+2.5:.1f}" '
                     f'stroke-linejoin="miter" opacity="0.55"/>')
        parts.append(f'<path d="{d}" fill="none" stroke="{pal["hot"]}" stroke-width="{w*0.45:.1f}" stroke-linejoin="miter"/>')
        if rng.random() < 0.6:
            bx, by = pts[len(pts) // 2]
            br = lightning(rng, bx, by, rng.uniform(30, 70), rng.choice((-1, 1)), 10)
            bd = "M" + " L".join(f"{a:.1f},{b:.1f}" for a, b in br)
            parts.append(f'<path d="{bd}" fill="none" stroke="{pal["fringe"]}" stroke-width="1.6" opacity="0.7"/>')
    return svg("".join(parts))


def glow_layer(pal):
    defs = ('<filter id="b" x="-20%" y="-200%" width="140%" height="500%"><feGaussianBlur stdDeviation="22"/></filter>'
            '<filter id="h" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="30"/></filter>')
    body = (f'<g filter="url(#b)"><ellipse cx="1150" cy="{CY}" rx="900" ry="38" fill="{pal["glow"]}" opacity="0.55"/>'
            f'<ellipse cx="1500" cy="{CY}" rx="520" ry="22" fill="{pal["mid"]}" opacity="0.6"/></g>'
            f'<g filter="url(#h)"><ellipse cx="1830" cy="{CY}" rx="150" ry="55" fill="{pal["mid"]}" opacity="0.9"/></g>')
    return svg(body, defs=defs)


def star(rng, cx, cy, spikes, r_out, r_in, amp, forward_bias):
    pts = []
    n = spikes * 2
    for i in range(n):
        a = i / n * 2 * math.pi + rng.uniform(-0.08, 0.08)
        fwd = 1 + forward_bias * max(0.0, math.cos(a)) ** 3
        r = (r_out * fwd * rng.uniform(0.6, 1.1)) if i % 2 == 0 else r_in * rng.uniform(0.8, 1.2)
        r += rng.uniform(-amp, amp)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in pts) + "Z"


def muzzle_layer(pal, seed=11):
    rng = random.Random(seed)
    c = 256
    parts = [
        f'<path d="{star(rng, c, c, 9, 150, 55, 6, 1.1)}" fill="{pal["rim"]}"/>',
        f'<path d="{star(rng, c, c, 9, 110, 42, 4, 1.0)}" fill="{pal["mid"]}"/>',
        f'<path d="{star(rng, c, c, 7, 70, 30, 3, 0.8)}" fill="{pal["hot"]}"/>',
        f'<circle cx="{c}" cy="{c}" r="22" fill="{pal["core"]}"/>',
    ]
    return svg("".join(parts), 512, 512)


def impact_layer(pal, seed=23):
    rng = random.Random(seed)
    c = 256
    parts = []
    # Broken shock ring: jagged arc segments.
    for k in range(7):
        a0 = k / 7 * 2 * math.pi + rng.uniform(0, 0.3)
        a1 = a0 + rng.uniform(0.35, 0.6)
        pts = []
        steps = 10
        for s in range(steps + 1):
            a = a0 + (a1 - a0) * s / steps
            r = 200 + rng.uniform(-9, 9)
            pts.append((c + r * math.cos(a), c + r * math.sin(a)))
        d = "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in pts)
        parts.append(f'<path d="{d}" fill="none" stroke="{pal["rim"]}" stroke-width="9" stroke-linejoin="miter"/>')
        parts.append(f'<path d="{d}" fill="none" stroke="{pal["hot"]}" stroke-width="3"/>')
    parts += [
        f'<path d="{star(rng, c, c, 11, 150, 60, 8, 0)}" fill="{pal["rim"]}"/>',
        f'<path d="{star(rng, c, c, 11, 112, 48, 5, 0)}" fill="{pal["mid"]}"/>',
        f'<path d="{star(rng, c, c, 8, 70, 34, 3, 0)}" fill="{pal["hot"]}"/>',
        f'<circle cx="{c}" cy="{c}" r="26" fill="{pal["core"]}"/>',
    ]
    return svg("".join(parts), 512, 512)


def spark_layer(pal, seed=5):
    """A miniature jagged streak, emitted by the impact as flying sparks."""
    rng = random.Random(seed)
    w, h, cy = 256, 64, 32
    parts = []
    for color, half, amp in ((pal["rim"], 9, 3), (pal["mid"], 6, 2), (pal["core"], 2.5, 0.6)):
        top, bot = [], []
        x = 8
        while x < 244:
            t = (x - 8) / 236
            base = half * (t ** 0.6 if t < 0.85 else math.sqrt(max(0, 1 - ((t - 0.85) / 0.15) ** 2)))
            top.append((x, cy - base - rng.uniform(-amp, amp)))
            bot.append((x, cy + base + rng.uniform(-amp, amp)))
            x += rng.uniform(8, 18)
        pts = [(8, cy)] + top + [(246, cy)] + bot[::-1]
        parts.append(f'<path d="M{" L".join(f"{a:.1f},{b:.1f}" for a, b in pts)}Z" fill="{color}"/>')
    return svg("".join(parts), w, h)


for name, pal in PALETTES.items():
    for f in range(FRAMES):
        (OUT / f"{name}_1_body_f{f}.svg").write_text(body_layer(pal, 100 + f))
        (OUT / f"{name}_2_fringe_f{f}.svg").write_text(fringe_layer(pal, 200 + f))
    (OUT / f"{name}_3_glow.svg").write_text(glow_layer(pal))
    (OUT / f"{name}_4_muzzle.svg").write_text(muzzle_layer(pal))
    (OUT / f"{name}_5_impact.svg").write_text(impact_layer(pal))
    (OUT / f"{name}_6_spark.svg").write_text(spark_layer(pal))

print("\n".join(sorted(p.name for p in OUT.iterdir())))
