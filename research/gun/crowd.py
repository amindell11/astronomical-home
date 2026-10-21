"""Crowd-scenario tables (2026-10-21). Usage: python crowd.py <run.jsonl>... [--by-weapon] [--lat]

Per tag and N: stand-in survival (mean restricted to the timeout, median, share that died),
damage taken per second, kills per minute, damage share by enemy weapon, and the enemies'
gun hit %, envelope % (time on target), in-range aim error and time to first hit.
"""
import json
import statistics
import sys
from collections import defaultdict


def load(paths):
    rows = []
    for p in paths:
        with open(p, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line:
                    r = json.loads(line)
                    if r.get("mode") == "crowd":
                        rows.append(r)
    return rows


def median_surv(rows):
    vals = sorted(r["survival"] if r["died"] else float("inf") for r in rows)
    m = statistics.median(vals) if vals else 0
    return ">T" if m == float("inf") else f"{m:.0f}"


def summarize(rows, weapon=None):
    lives = [l for r in rows for l in r["lives"] if weapon is None or l["weapon"].startswith(weapon)]
    guns = [l for l in lives if not l["weapon"].startswith("Missile")]
    mis = [l for l in lives if l["weapon"].startswith("Missile")]
    time = sum(r["survival"] for r in rows)
    dmg = sum(r["damageTaken"] for r in rows)
    kills = sum(r["kills"] for r in rows)
    shots = sum(l["shots"] for l in guns)
    hits = sum(l["hits"] for l in guns)
    mshots = sum(l["shots"] for l in mis)
    mhits = sum(l["hits"] for l in mis)
    steps = sum(l["steps"] for l in lives)
    env = sum(l["envelopeSteps"] for l in guns)
    envin = sum(l.get("inRangeEnvelopeSteps", 0) for l in guns)
    gsteps = sum(l["steps"] for l in guns)
    inr = sum(l["inRangeSteps"] for l in guns)
    err = sum(l["inRangeAimErr"] * l["inRangeSteps"] for l in guns if l["inRangeSteps"] > 0)
    ttfh = [l["firstHitT"] - max(0.0, l["firstInRangeT"]) for l in guns if l["firstHitT"] >= 0 and l["firstInRangeT"] >= 0]
    reached = [l for l in guns if l["firstInRangeT"] >= 0]
    eashots = [l for l in lives if l["eaErrAtShot"] >= 0]
    ea_err = (sum(l["eaErrAtShot"] * l["shots"] for l in eashots) / max(1, sum(l["shots"] for l in eashots))) if eashots else -1
    ea_prog = (sum(l["eaProgAtShot"] * l["shots"] for l in eashots) / max(1, sum(l["shots"] for l in eashots))) if eashots else -1
    life_min = steps * 0.02 / 60
    return {
        "eps": len(rows),
        "died": sum(r["died"] for r in rows) / max(1, len(rows)),
        "rmst": time / max(1, len(rows)),
        "median": median_surv(rows),
        "dps": dmg / max(1e-6, time),
        "kpm": kills / max(1e-6, time) * 60,
        "shareL": sum(r["dmgLasers"] for r in rows) / max(1e-6, dmg),
        "shareR": sum(r["dmgRippers"] for r in rows) / max(1e-6, dmg),
        "shareM": sum(r["dmgMissiles"] for r in rows) / max(1e-6, dmg),
        "shareC": sum(r["dmgCollision"] for r in rows) / max(1e-6, dmg),
        "hit": hits / max(1, shots),
        "mhit": mhits / max(1, mshots),
        "env": env / max(1, gsteps),
        "envIn": envin / max(1, inr),
        "err": err / max(1, inr),
        "ttfh": statistics.median(ttfh) if ttfh else -1,
        "hitLives": len(ttfh) / max(1, len(reached)),
        "eaErr": ea_err,
        "eaProg": ea_prog,
        "jinkPerMin": sum(l["eaJinks"] for l in lives) / max(1e-6, life_min),
        "standHit": sum(r["standInHits"] for r in rows) / max(1, sum(r["standInShots"] for r in rows)),
        "boosts": sum(r["standInBoosts"] for r in rows) / max(1e-6, time) * 60,
    }


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    by_weapon = "--by-weapon" in sys.argv
    lat = "--lat" in sys.argv
    rows = load(args)
    groups = defaultdict(list)
    order = []
    for r in rows:
        k = (r["tag"], r["n"])
        if k not in groups:
            order.append(k)
        groups[k].append(r)
    print("| Tag | N | Eps | Died | Survival mean (s) | Median | Dmg taken/s | Kills/min | Share L/R/M/C | Gun hit % | Missile hit % | Envelope % | Env % in range | Aim err in range | TTFH (s) | Lives that hit | EA err at shot | EA prog | Jinks/min | Stand hit % | Boosts/min |")
    print("|" + "---|" * 21)
    for k in order:
        s = summarize(groups[k])
        print(f"| {k[0]} | {k[1]} | {s['eps']} | {s['died']:.0%} | {s['rmst']:.0f} | {s['median']} | {s['dps']:.2f} | {s['kpm']:.1f} | "
              f"{s['shareL']:.0%}/{s['shareR']:.0%}/{s['shareM']:.0%}/{s['shareC']:.0%} | {s['hit']:.0%} | {s['mhit']:.0%} | "
              f"{s['env']:.0%} | {s['envIn']:.0%} | {s['err']:.1f} | {s['ttfh']:.1f} | {s['hitLives']:.0%} | "
              f"{s['eaErr']:.1f} | {s['eaProg']:.2f} | {s['jinkPerMin']:.1f} | {s['standHit']:.0%} | {s['boosts']:.1f} |")
        if by_weapon:
            for w in ("Lasers", "Rippers", "Missiles"):
                sw = summarize(groups[k], w)
                print(f"|   {w} | | | | | | | | | {sw['hit']:.0%} | {sw['mhit']:.0%} | {sw['env']:.0%} | {sw['envIn']:.0%} | {sw['err']:.1f} | {sw['ttfh']:.1f} | {sw['hitLives']:.0%} | | | | | |")
    if lat:
        by = defaultdict(lambda: [0] * 31)
        for r in rows:
            h = by[r["tag"]]
            for i, c in enumerate(r["latHist"]):
                h[i] += c
        print("| Tag | " + " | ".join(f">{t}" for t in (2, 3, 4, 5, 6, 8, 10, 15)) + " |")
        for tag, h in by.items():
            total = max(1, sum(h))
            print(f"| {tag} | " + " | ".join(f"{sum(h[t:]) / total:.1%}" for t in (2, 3, 4, 5, 6, 8, 10, 15)) + " |")


if __name__ == "__main__":
    main()
