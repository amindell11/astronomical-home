"""Summarize gun-research JSONL runs.

Usage: python analyze.py <run.jsonl> [<run.jsonl> ...]
Matrix runs print a win-rate matrix (row weapon's score vs column weapon; draws count 0.5) and
per-weapon aggregates. Marks runs print per weapon x target rows against the ceiling table.
"""
import json
import sys
from collections import defaultdict

# Sustained DPS at full accuracy, one mount, from prefab values on main 39eb522ef (see README).
CEILING = {
    "Lasers": 37.5,        # heat-capped: 20 dmg / 0.533 s; burst 66.7 for 13 shots
    "TurboLasers": 19.8,   # vent-to-cold then 19-shot burst; burst 66.7 for 19 shots
    "Rippers": 36.9,       # 144 per 2.4 s dump + 1.5 s reload; burst 60
    "ChargeLasers": 42.9,  # tap at 1/3 charge every 0.42 s (18 dmg); full-charge cycle 25.0
    "Railgun": 30.0,       # 45 per 1.5 s, charge overlaps cooldown
    "Missiles": 6.7,       # 2 rounds x 50 (35 + 15 splash) per 15 s regen; burst 100
    "Grenades": 8.75,      # 3 x 35 per 12 s regen; burst 105
}
WEAPONS = list(CEILING)


def load(path):
    rows = []
    with open(path, encoding="utf-8-sig") as f:
        for line in f:
            line = line.strip().lstrip("﻿")
            if line:
                rows.append(json.loads(line))
    return rows


def side_dps(side, duration):
    return side["tally"]["damageDealt"] / duration if duration > 0 else 0.0


def matrix(rows, label):
    score = defaultdict(lambda: [0.0, 0])
    ttk = defaultdict(list)
    agg = defaultdict(lambda: defaultdict(float))
    for r in rows:
        a, b = r["a"], r["b"]
        wa, wb = a["weapon"], b["weapon"]
        w = r["winner"]
        sa = 1.0 if w == "A" else 0.0 if w == "B" else 0.5
        score[(wa, wb)][0] += sa
        score[(wa, wb)][1] += 1
        if wa != wb:
            score[(wb, wa)][0] += 1.0 - sa
            score[(wb, wa)][1] += 1
        if w in ("A", "B"):
            ttk[(wa, wb)].append(r["duration"])
        for side, opp_w in ((a, wb), (b, wa)):
            g = agg[side["weapon"]]
            t = side["tally"]
            g["n"] += 1
            g["time"] += r["duration"]
            g["dmg"] += t["damageDealt"]
            g["fired"] += t["fired"]
            g["hits"] += t["hits"]
            g["env"] += t["envelopeSteps"]
            g["steps"] += max(1, side["steps"])
            g["aimErr"] += side["meanAimErr"] * side["steps"]
            g["aimOk"] += side["aimOkFrac"] * side["steps"]
            g["dist"] += side["meanDist"] * side["steps"]
            g["ready"] += side["readyFrac"] * side["steps"]
            g["charging"] += side["chargingFrac"] * side["steps"]
            g["overheat"] += side["overheatFrac"] * side["steps"]
            ir = side.get("inRangeFrac", 0) * side["steps"]
            if side.get("inRangeAimErr", -1) >= 0:
                g["irErr"] += side["inRangeAimErr"] * ir
                g["irOk"] += side.get("inRangeAimOkFrac", 0) * ir
                g["ir"] += ir
            if opp_w != side["weapon"]:
                g["xn"] += 1
                won = (w == "A" and side is a) or (w == "B" and side is b)
                g["xw"] += 1.0 if won else 0.5 if w == "draw" else 0.0
            if w == "draw":
                g["draws"] += 1
    present = [x for x in WEAPONS if x in agg] + sorted(x for x in agg if x not in WEAPONS)
    print(f"\n## {label}: win rate of row vs column (draw = 0.5), n per cell")
    head = "| row \\ col | " + " | ".join(present) + " | mean vs others |"
    print(head)
    print("|" + "---|" * (len(present) + 2))
    for x in present:
        cells = []
        for y in present:
            s, n = score.get((x, y), (0, 0))
            cells.append(f"{s / n:.2f} ({n})" if n else "-")
        g = agg[x]
        cells.append(f"{g['xw'] / g['xn']:.2f}" if g["xn"] else "-")
        print(f"| {x} | " + " | ".join(cells) + " |")
    print(f"\n## {label}: per weapon (all its fights)")
    print("| weapon | win vs others | draws | DPS dealt | ceiling | % ceiling | shots/s | hit % | envelope % | aim ok % | mean aim err | mean dist | ready % | charging % | in-range % | in-range aim err | in-range aim ok % |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for x in present:
        g = agg[x]
        dps = g["dmg"] / g["time"] if g["time"] else 0
        ceil = CEILING.get(x, 0)
        print(f"| {x} | {g['xw'] / g['xn'] if g['xn'] else 0:.2f} | {int(g['draws'])} | {dps:.1f} | {ceil} | {100 * dps / ceil if ceil else 0:.0f}% | "
              f"{g['fired'] / g['time'] if g['time'] else 0:.2f} | {100 * g['hits'] / g['fired'] if g['fired'] else 0:.0f}% | "
              f"{100 * g['env'] / g['steps']:.0f}% | {100 * g['aimOk'] / g['steps']:.0f}% | {g['aimErr'] / g['steps']:.1f} | "
              f"{g['dist'] / g['steps']:.1f} | {100 * g['ready'] / g['steps']:.0f}% | {100 * g['charging'] / g['steps']:.0f}% | "
              f"{100 * g['ir'] / g['steps']:.0f}% | {g['irErr'] / g['ir'] if g['ir'] else float('nan'):.1f} | {100 * g['irOk'] / g['ir'] if g['ir'] else 0:.0f}% |")


def marks(rows, label):
    agg = defaultdict(lambda: defaultdict(float))
    targets = []
    for r in rows:
        w, t = r["pairing"].split("|")
        if t not in targets:
            targets.append(t)
        g = agg[(w, t)]
        a = r["a"]
        tal = a["tally"]
        g["n"] += 1
        g["kills"] += 1 if r["winner"] == "A" else 0
        if r["winner"] == "A":
            g["ttk"] += r["duration"]
        g["time"] += r["duration"]
        g["dmg"] += tal["damageDealt"]
        g["fired"] += tal["fired"]
        g["hits"] += tal["hits"]
        g["env"] += tal["envelopeSteps"]
        g["steps"] += max(1, a["steps"])
        g["aimOk"] += a["aimOkFrac"] * a["steps"]
        g["aimErr"] += a["meanAimErr"] * a["steps"]
        g["dist"] += a["meanDist"] * a["steps"]
        g["selfHits"] += tal["selfHits"]
        g["full"] += tal.get("fullChargeShots", 0)
        g["early"] += tal.get("earlyShots", 0)
        g["dropped"] += tal.get("droppedCharges", 0)
        g["guided"] += tal.get("guidedLaunches", 0)
    print(f"\n## {label}: shooter vs unarmed target")
    print("| weapon | target | kills/n | mean TTK | DPS | % ceiling | shots/s | hit % | envelope % | aim ok % | aim err | dist | full/early/dropped | guided | self hits |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    weapons = [x for x in WEAPONS if any(k[0] == x for k in agg)]
    for w in weapons:
        for t in targets:
            g = agg.get((w, t))
            if not g:
                continue
            dps = g["dmg"] / g["time"] if g["time"] else 0
            ceil = CEILING.get(w, 0)
            ttk = g["ttk"] / g["kills"] if g["kills"] else float("nan")
            print(f"| {w} | {t} | {int(g['kills'])}/{int(g['n'])} | {ttk:.1f} | {dps:.1f} | {100 * dps / ceil if ceil else 0:.0f}% | "
                  f"{g['fired'] / g['time']:.2f} | {100 * g['hits'] / g['fired'] if g['fired'] else 0:.0f}% | {100 * g['env'] / g['steps']:.0f}% | "
                  f"{100 * g['aimOk'] / g['steps']:.0f}% | {g['aimErr'] / g['steps']:.1f} | {g['dist'] / g['steps']:.1f} | "
                  f"{int(g['full'])}/{int(g['early'])}/{int(g['dropped'])} | {int(g['guided'])} | {int(g['selfHits'])} |")


def main():
    for path in sys.argv[1:]:
        rows = load(path)
        if not rows:
            print(f"{path}: empty")
            continue
        label = rows[0]["tag"]
        if rows[0]["mode"] == "marks":
            marks(rows, label)
        else:
            matrix(rows, label)


if __name__ == "__main__":
    main()
