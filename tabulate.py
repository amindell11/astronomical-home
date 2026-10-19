import json
import sys

rows = [json.loads(l) for l in open(sys.argv[1], encoding="utf-8") if l.strip()]
cur = None
for r in rows:
    if r.get("header"):
        print("HEADER", r)
        continue
    if "matrix" in r:
        cur = (r["matrix"], r["screen"])
        print()
        print("=== %s @ %s  cameras: %s" % (r["matrix"], r["screen"], r["cameras"]))
        continue
    if "passes" in r:
        print("  passes:", r["passes"][:900])
        continue
    if "label" in r:
        print("  %-62s gpu %6.2f (p90 %6.2f)  main %6.2f  cpuFrame %6.2f  presentWait %6.2f  dt %6.2f  rs %s msaa %s rt %s" % (
            r["label"][:62], r["gpu_med"], r["gpu_p90"], r["main_med"], r["cpuFrame_med"],
            r["presentWait_med"], r["dt_med"], r["renderScale"], r["msaa"], r["previewRT"]))
