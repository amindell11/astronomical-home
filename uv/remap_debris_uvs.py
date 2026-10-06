"""Scratch (evidence only): remap UV0 of Crimson's saved debris meshes from the old atlas layout to #731's.

Each debris vertex's old UV is located in an old evaluated UV triangle of the Blender source and
carried barycentrically into the matching new triangle. Only the UV0 bytes of each mesh change.
Usage: python remap_debris_uvs.py <uv_tris.npz> <breakup dir> <old atlas> <new atlas> [--write]
"""
import sys, re, glob, os, json
import numpy as np
from PIL import Image

npz, breakup, old_atlas, new_atlas = sys.argv[1:5]
write = "--write" in sys.argv
data = np.load(npz)
parts = sorted(k.split("|", 1)[1] for k in data.files if k.startswith("old|"))
old = np.concatenate([data[f"old|{p}"] for p in parts]).astype(np.float64)   # (T,3,2)
new = np.concatenate([data[f"new|{p}"] for p in parts]).astype(np.float64)
owner = np.concatenate([[i] * len(data[f"old|{p}"]) for i, p in enumerate(parts)])
area = np.abs((old[:, 1, 0] - old[:, 0, 0]) * (old[:, 2, 1] - old[:, 0, 1]) - (old[:, 2, 0] - old[:, 0, 0]) * (old[:, 1, 1] - old[:, 0, 1]))
keep = area > 1e-14
old, new, owner = old[keep], new[keep], owner[keep]

G = 256
lo, hi = old.min(1), old.max(1)
cells = {}
for t in range(len(old)):
    x0, y0 = np.clip((lo[t] * G).astype(int), 0, G - 1); x1, y1 = np.clip((hi[t] * G).astype(int), 0, G - 1)
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            cells.setdefault((x, y), []).append(t)

def bary(p, tri):
    a, b, c = tri
    v0, v1, v2 = b - a, c - a, p - a
    d = v0[0] * v1[1] - v1[0] * v0[1]
    l1 = (v2[0] * v1[1] - v1[0] * v2[1]) / d
    l2 = (v0[0] * v2[1] - v2[0] * v0[1]) / d
    return np.array([1 - l1 - l2, l1, l2])

def remap(uv):
    x, y = np.clip((uv * G).astype(int), 0, G - 1)
    best, cands = None, []
    for t in cells.get((x, y), []):
        w = bary(uv, old[t])
        slack = -w.min()
        if slack <= 1e-5:
            cands.append(w @ new[t])
        if best is None or slack < best[0]:
            best = (slack, t, w)
    if cands:
        c = np.array(cands)
        return c[0], float(np.abs(c - c[0]).max()), 0.0
    if best is None:
        return None, 0.0, None
    slack, t, w = best
    w = np.clip(w, 0, None); w /= w.sum()
    return w @ new[t], 0.0, slack

old_img = np.asarray(Image.open(old_atlas).convert("RGB"), np.float64)
new_img = np.asarray(Image.open(new_atlas).convert("RGB"), np.float64)
def sample(img, uv):
    h, w = img.shape[:2]
    x = np.clip((uv[:, 0] * w).astype(int), 0, w - 1); y = np.clip(((1 - uv[:, 1]) * h).astype(int), 0, h - 1)
    return img[y, x]

# Control: the same colour metric on the source's own triangles (centroids), i.e. what the rebake alone changes.
ctrl = np.abs(sample(old_img, old.mean(1)) - sample(new_img, new.mean(1))).mean(1)
report = {"control_triangle_centroids": {"n": int(len(ctrl)), "mean": float(ctrl.mean()), "p95": float(np.percentile(ctrl, 95))}, "meshes": {}}

for path in sorted(glob.glob(os.path.join(breakup, "*.asset"))):
    text = open(path, encoding="utf-8", newline="").read()
    vd = text[text.index("m_VertexData:"):]
    count = int(re.search(r"m_VertexCount: (\d+)", vd).group(1))
    chans = re.findall(r"- stream: (\d+)\n\s+offset: (\d+)\n\s+format: (\d+)\n\s+dimension: (\d+)", vd)
    assert all(s == "0" for s, *_ in chans), path
    stride = sum(4 * int(d) for s, o, f, d in chans)
    s4, o4, f4, d4 = map(int, chans[4]); assert (f4, d4) == (0, 2), path
    m = re.search(r"_typelessdata: ([0-9a-f]+)", text)
    raw = bytearray(bytes.fromhex(m.group(1))); assert len(raw) == count * stride, path
    buf = np.frombuffer(bytes(raw), np.uint8).reshape(count, stride)
    uv = buf[:, o4:o4 + 8].copy().view(np.float32).reshape(count, 2).astype(np.float64)
    out = np.empty_like(uv); misses = 0; worst_disagree = 0.0; snapped = []; unmapped = 0
    cache = {}
    for i, p in enumerate(uv):
        key = (p[0], p[1])
        if key not in cache:
            cache[key] = remap(p)
        q, dis, slack = cache[key]
        if q is None:
            unmapped += 1; q = p
        if slack: snapped.append(slack)
        worst_disagree = max(worst_disagree, dis)
        out[i] = q
    diff = np.abs(sample(old_img, uv) - sample(new_img, out)).mean(1)
    name = os.path.basename(path)
    report["meshes"][name] = {"vertices": count, "unmapped": unmapped, "snapped": len(snapped),
                              "max_snap": float(max(snapped)) if snapped else 0.0,
                              "max_candidate_disagreement": worst_disagree,
                              "colour_mean": float(diff.mean()), "colour_p95": float(np.percentile(diff, 95))}
    if write:
        newbuf = buf.copy(); newbuf[:, o4:o4 + 8] = out.astype(np.float32).view(np.uint8).reshape(count, 8)
        before = buf.copy(); before[:, o4:o4 + 8] = 0; after = newbuf.copy(); after[:, o4:o4 + 8] = 0
        assert np.array_equal(before, after)
        text = text[:m.start(1)] + newbuf.tobytes().hex() + text[m.end(1):]
        open(path, "w", encoding="utf-8", newline="").write(text)
print(json.dumps(report, indent=1))
