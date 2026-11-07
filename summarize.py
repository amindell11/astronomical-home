import json, sys

def load(p):
    rows = []
    with open(p, encoding="utf-8") as f:
        for l in f:
            l = l.strip()
            if l:
                rows.append(json.loads(l))
    return rows

def main():
    for p in sys.argv[1:]:
        rows = load(p)
        print("=" * 100)
        print(p)
        for r in rows:
            if "header" in r or "flight" in r or "error" in r or "done" in r:
                print("  ", {k: v for k, v in r.items()})
        print("\n  LIVE (dt = frame interval ms; main/render/gpu = FrameTimingManager ms)")
        for r in rows:
            if "live" in r:
                print("  %-16s %-9s prof=%-5s n=%5d  dt med %6.2f p95 %6.2f p99 %6.2f max %7.1f | >16.7 %4d >25 %4d >33 %4d >50 %3d | main med %5.2f p95 %5.2f p99 %5.2f | rend %5.2f | gpu med %5.2f p95 %5.2f | gcKB med %5.1f p95 %6.1f tot %8.0f coll %d" % (
                    r["live"], r["screen"], r["profiled"], r["frames"], r["dt_med"], r["dt_p95"], r["dt_p99"], r["dt_max"],
                    r["over16"], r["over25"], r["over33"], r["over50"], r["main_med"], r["main_p95"], r["main_p99"], r["render_med"],
                    r["gpu_med"], r["gpu_p95"], r["gcKB_med"], r["gcKB_p95"], r["gcKB_total"], r["gcCollections"]))
                print("      worst:", r["worst"])
                print("      census before:", r["censusBefore"])
                print("      census after: ", r["censusAfter"])
        print("\n  FROZEN / ARMS (GPU median ms)")
        for r in rows:
            if "matrix" in r:
                print("   --- matrix", r["matrix"], r["screen"], "|", r["census"])
                print("       sky:", r["sky"])
                print("       cams:", r["cameras"])
                print("       features:", r["features"])
            if "label" in r:
                print("   %-75s %-9s gpu %6.2f (p90 %6.2f) main %6.2f rend %6.2f dt %6.2f" % (
                    r["label"][:75], r["screen"], r["gpu_med"], r["gpu_p90"], r["main_med"], r["render_med"], r["dt_med"]))
            if "passes" in r:
                print("   passes", r["screen"], ":", r["passes"][:900])

main()
