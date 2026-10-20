"""Flight probe summary: whole-frame GPU ms per locale x quality x resolution, and the 4K sky split."""
import json
import statistics
import sys
from collections import defaultdict

cells = defaultdict(dict)  # (locale, quality, screen) -> label -> row
meta = []
for path in sys.argv[1:]:
    for line in open(path, encoding="utf-8"):
        if not line.strip():
            continue
        r = json.loads(line)
        if "label" in r:
            cells[(r["locale"], r["quality"], r["screen"])][r["label"]] = r
        elif "header" in r or "flight" in r or "matrix" in r or "error" in r or "liveScene" in r:
            meta.append(r)


def g(cell, label):
    row = cell.get(label)
    return row["gpu_med"] if row else None


def fmt(x):
    return "-" if x is None else "%.1f" % x


def base(cell):
    xs = [g(cell, k) for k in ("base", "base_again", "base_end") if g(cell, k) is not None]
    return statistics.median(xs), min(xs), max(xs)


for m in meta:
    if "passes" not in m:
        print("META", json.dumps(m)[:700])
print()

locales = sorted({k[0] for k in cells})
print("## Whole frame, GPU ms (fps): frozen base [min-max of base/base_again/base_end] | live median")
for loc in locales:
    print("###", loc)
    for q in ("Performant", "Balanced", "High Fidelity"):
        row = []
        for s in ("1920x1080", "3840x2160"):
            c = cells.get((loc, q, s))
            if not c:
                continue
            b, lo, hi = base(c)
            live = [g(c, k) for k in ("live_base", "live_base_again") if g(c, k) is not None]
            lv = statistics.median(live) if live else None
            row.append("%s: %.1f (%d) [%.1f-%.1f] | live %s, live_no_sky %s" % (
                s, b, round(1000 / b), lo, hi, fmt(lv), fmt(g(c, "live_no_sky"))))
        if row:
            print("  %-14s %s" % (q, "   ||   ".join(row)))
print()

for s in ("3840x2160", "1920x1080"):
    print("## Split at %s, GPU ms: leave-one-out (base_again - no_sky:X) / alone (only_sky:X - no_sky)" % s)
    for loc in locales:
        for q in ("Performant", "Balanced", "High Fidelity"):
            c = cells.get((loc, q, s))
            if not c:
                continue
            b, _, _ = base(c)
            ba = g(c, "base_again") or b
            print("### %s %s %s  base %.2f  no_sky %.2f  floor(sky/ui/post off) %s" % (
                loc, q, s, b, g(c, "no_sky"), fmt(g(c, "floor_sky_ui_post_off"))))
            print("  All sky layers: %.2f" % (b - g(c, "no_sky")))
            layers = sorted({k.split(":", 1)[1] for k in c if k.startswith("no_sky:")})
            for layer in layers:
                loo = ba - g(c, "no_sky:" + layer)
                alone = g(c, "only_sky:" + layer)
                alone = None if alone is None else alone - g(c, "no_sky")
                print("  %-60s %6.2f / %s" % (layer.split("<")[0], loo, fmt(alone)))
            print("  post (base - no_post): %.2f" % (b - g(c, "no_post")))
            print("  ui (base - no_any_ui): %.2f" % (b - g(c, "no_any_ui")))
            for k in sorted(c):
                if k.startswith("no_cam:"):
                    print("  cam %s: %.2f" % (k[7:], b - g(c, k)))
            for k in ("renderScale_1", "renderScale_0.5", "msaa_1", "sky_on_no_ui_no_post", "base_tierVsync"):
                if k in c:
                    print("  %s: %.2f" % (k, g(c, k)))
    print()
