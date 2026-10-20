"""Statistical comparison of the live 2D cloud-bank model vs the 4D-torus tile model (Locale_3 FarNebula)."""
import numpy as np

rng = np.random.default_rng(3)
M = 1 << 22
TABLE = rng.random(M)
PRIMES = np.array([73856093, 19349663, 83492791, 2654435761], dtype=np.int64)
SEED = 17.0; COVER = 0.58; STRETCH = 2.4; SCALE = 0.24; GLOW = 0.7; SPREAD = 0.2; STRENGTH = 0.7; OPACITY = 0.94
COOL = np.array([0.1718, 0.3734, 0.9547]); WARM = np.array([0.4100, 0.2601, 0.9309])
SHADOW = np.array([0.2091, 0.2427, 0.5321]); GCOOL = np.array([0.06, 0.55, 1.5]); GWARM = np.array([0.55, 0.09, 1.5])


def ss(t): return t * t * (3 - 2 * t)
def smoothstep(a, b, x): return ss(np.clip((x - a) / (b - a), 0, 1))
def lookup(cells): return TABLE[(cells @ PRIMES[:cells.shape[1]]) % M]


def noise2(p):
    c = np.floor(p).astype(np.int64); f = ss(p - c); out = np.zeros(len(p))
    for k in range(4):
        o = np.array([k & 1, (k >> 1) & 1]); w = np.prod(np.where(o == 1, f, 1 - f), axis=1)
        out += lookup(c + o) * w
    return out


def noise4(p):
    c = np.floor(p).astype(np.int64); f = ss(p - c); out = np.zeros(len(p))
    for k in range(16):
        o = np.array([(k >> i) & 1 for i in range(4)]); w = np.prod(np.where(o == 1, f, 1 - f), axis=1)
        out += lookup(c + o) * w
    return out


def tile_noise(p, period, offset):
    a = p / period * 2 * np.pi; r = period / (2 * np.pi)
    torus = np.stack([np.cos(a[:, 0]) * r[0], np.sin(a[:, 0]) * r[0], np.cos(a[:, 1]) * r[1], np.sin(a[:, 1]) * r[1]], 1)
    return np.clip(0.5 + (noise4(torus + offset) - 0.5) * 1.346, 0, 1)


def fractal2(p):
    v = 0; w = 0.5
    for _ in range(5):
        v = v + noise2(p) * w; p = p * 2.03 + np.array([7.1, 13.7]); w *= 0.5
    return v


def fractal4(p, period, offset):
    v = 0; w = 0.5; period = np.array(period, float); offset = np.array(offset, float)
    for _ in range(5):
        v = v + tile_noise(p, period, offset) * w
        p = p * 2.03; period = period * 2.03; offset = offset + np.array([7.1, 13.7, 5.3, 11.9]); w *= 0.5
    return v


def shade(broad, detail, violet_f, pockets_n, hue_n, shadow_f):
    field = broad + (detail - 0.5) * 0.16; thr = 1 - COVER
    coverage = smoothstep(thr - 0.09, thr + 0.12, field)
    terr = field * 0.65 + np.floor(field * 24) / 24 * 0.35
    light = smoothstep(thr + 0.02, thr + 0.30, terr) ** 2.5
    violet = np.clip(1 - np.abs(violet_f - 0.5) * 10, 0, 1) ** 1.5
    color = SHADOW + (COOL - SHADOW) * light[:, None]
    target = WARM * (0.2 + light[:, None]); color = color + (target - color) * (violet * 0.25)[:, None]
    color = color + WARM * (violet * np.sqrt(light) * 0.15)[:, None]
    color *= STRENGTH; opacity = coverage * OPACITY
    env = smoothstep(thr - SPREAD, thr + 0.18, broad)
    pockets = smoothstep(0.4, 0.88, pockets_n) ** 2; pockets *= 0.5 + 2.5 * pockets ** 3
    hue = smoothstep(0.5, 0.75, hue_n)
    glow = SHADOW * 0.25 + (GCOOL + (GWARM - GCOOL) * hue[:, None]) * pockets[:, None]
    ls = smoothstep(0.36, 0.4, shadow_f) * 0.22 + smoothstep(0.44, 0.48, shadow_f) * 0.3 + smoothstep(0.52, 0.56, shadow_f) * 0.35
    glow *= (env * (1 + (0.2 - 1) * ls) * GLOW * STRENGTH)[:, None]
    rgb = color * opacity[:, None] + glow
    return rgb, opacity, light, violet, hue


def live(grain):
    p = grain * np.array([1 / STRETCH, 1]) + SEED * np.array([13.7, 29.3])
    warp = np.stack([fractal2(p * 0.7), fractal2(p * 0.7 + 19.3)], 1) - 0.5
    cloud = p + warp * 2.4
    return shade(fractal2(cloud), fractal2(grain * 4 + warp * 4), fractal2(p * 1.4 + 71.2),
                 noise2(cloud * 1.6 + np.array([9.1, 41.3])), noise2(p * 0.8 + 71.2), fractal2(grain * 1.4 + 25.7))


def tiled(grain, period, seed4):
    s = np.array([1 / STRETCH, 1]); p = grain * s; pp = period * s
    warp = np.stack([fractal4(p * 0.7, pp * 0.7, seed4), fractal4(p * 0.7, pp * 0.7, seed4 + 19.3)], 1) - 0.5
    cloud = p + warp * 2.4
    return shade(fractal4(cloud, pp, seed4), fractal4(grain * 4 + warp * 4, period * 4, np.zeros(4)),
                 fractal4(p * 1.4, pp * 1.4, seed4 + 71.2), tile_noise(cloud * 1.6, pp * 1.6, seed4 + np.array([9.1, 41.3, 3.7, 23.1])),
                 tile_noise(p * 0.8, pp * 0.8, seed4 + 71.2), fractal4(grain * 1.4, period * 1.4, np.full(4, 25.7)))


def report(name, out):
    rgb, op, light, violet, hue = out
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722])
    print(f"{name:28s} opacity {op.mean():.3f}  light {light.mean():.3f}  violet {violet.mean():.4f}  warmHue {hue.mean():.3f}  "
          f"lum {lum.mean():.3f}  lum>0.3 {np.mean(lum > 0.3):.3f}")


n = 120_000
report("live 2D (wide area)", live(rng.random((n, 2)) * 2000 * SCALE))
period = np.array([53 * SCALE, 53 * SCALE])
for k, seed in enumerate([SEED * np.array([13.7, 29.3, 7.9, 17.3])] + [rng.random(4) * 500 for _ in range(3)]):
    report(f"torus tile (seed {'mat' if k == 0 else k})", tiled(rng.random((n, 2)) * period, period, seed))
