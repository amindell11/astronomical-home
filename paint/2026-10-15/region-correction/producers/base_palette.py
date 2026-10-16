import numpy as np

def rgb(key):
    h = PAL.get(key, key)
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)], dtype=np.float32)

def linear(color):
    c = np.asarray(color)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)

def convex_hull(points):
    pts = sorted(set((tuple(p) for p in points)))

    def cross(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    lower = []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    upper = []
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return [list(p) for p in lower[:-1] + upper[:-1]]

def segment_distance(q, a, b):
    a = np.asarray(a)
    b = np.asarray(b)
    d = b - a
    t = np.clip(np.sum((q - a) * d, axis=1) / np.dot(d, d), 0, 1)
    return np.linalg.norm(q - a - t[:, None] * d, axis=1)

def path_distance(q, points):
    value = np.full(len(q), 100.0, dtype=np.float32)
    for a, b in zip(points[:-1], points[1:]):
        value = np.minimum(value, segment_distance(q, a, b))
    return value

def inside(q, points):
    hit = np.zeros(len(q), dtype=bool)
    for a, b in zip(points, points[1:] + points[:1]):
        if abs(b[1] - a[1]) < 1e-12:
            continue
        hit ^= ((a[1] > q[:, 1]) != (b[1] > q[:, 1])) & (q[:, 0] < (b[0] - a[0]) * (q[:, 1] - a[1]) / (b[1] - a[1]) + a[0])
    return hit

def paint(name, p, normal):
    x, y, z = p.T
    ax = np.abs(x)
    q = p[:, :2]
    n = len(p)
    upper = normal[2] > 0.28
    lower = normal[2] < -0.28
    color = np.tile(rgb('ivory'), (n, 1))
    if lower:
        color[:] = rgb('belly')
    elif not upper and name not in ('Tail', 'Power Nacelle Housing'):
        color[:] = rgb('belly') * 0.88
    if name == 'wing_armature':
        color[:] = rgb('dark') if upper else rgb('belly') * 0.82
    if name == 'Sparrow Tail':
        color[:] = rgb('ivory') if upper else rgb('dark')
    if name == 'Tail':
        color[:] = rgb('ivory')
        color[(y < -0.61) & (z > 0.277)] = rgb('orange')
    if name == 'wing_tail':
        color[:] = rgb('orange') if upper or lower else rgb('dark')
    if name == 'Fuselage' and upper:
        stripe = (y < -0.25) & (y > -0.67)
        color[stripe] = rgb('orange')
        color[stripe & ((np.abs(y + 0.525) < 0.011) | (np.abs(y + 0.596) < 0.011))] = rgb('ivory')
    if name in ('Wing', 'wing_armor') and (upper or lower):
        poly = wing_polygons[name]
        distance = path_distance(np.column_stack([ax, y]), [poly[-2], poly[-1], poly[0]])
        orange = distance < 0.08 if name == 'Wing' else np.zeros(n, dtype=bool)
        if lower and name == 'Wing':
            color[:] = rgb('ivory') * 0.83
        color[orange] = rgb('orange')
        if upper:
            clear = (ax - nx) ** 2 + (y - ny) ** 2 < 0.079 ** 2
            color[clear] = rgb('ivory')
    if name == 'Cockpit.001' and upper:
        ring_distance = path_distance(q, canopy_outline + [canopy_outline[0]])
        ring = ring_distance < 0.02
        color[ring] = rgb('orange')
        color[(ring_distance > 0.019) & (ring_distance < 0.021)] = rgb('ink')
    if name == 'Power Nacelle Housing':
        color[:] = rgb('ivory') if normal[2] > 0.55 else rgb('dark')
        tick = (np.abs(y - (ny - 0.057)) < 0.008) & (np.abs(ax - nx) < 0.003)
        color[tick] = rgb('ink')
    for entry in design:
        if name not in entry['objects']:
            continue
        if entry['side'] == 'top' and (not upper):
            continue
        if entry['side'] == 'bottom' and (not lower):
            continue
        if entry['kind'] == 'line':
            mask = path_distance(q, entry['points']) < entry['width'] / 2
        else:
            mask = inside(q, entry['points'])
        alpha = entry['opacity']
        color[mask] = color[mask] * (1 - alpha) + rgb(entry['color']) * alpha
    variation = 1 + 0.005 * np.sin(x * 51 + y * 38) + 0.003 * np.sin(x * 93 - y * 17)
    return np.clip(color * variation[:, None], 0, 1)
