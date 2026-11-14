"""Outnumbered sweep table from matrix-v3 rows."""
import glob
import json
import sys

root = sys.argv[1]


def load(name):
    files = glob.glob(f"{root}/{name}/*/*-matrix.jsonl")
    return [json.loads(line) for line in open(files[0]) if line.strip()] if files else None


def stats(rows):
    n = len(rows)
    sec = sum(r["simSeconds"] for r in rows)
    kills = sum(r["enemiesKilled"] for r in rows)
    by_seed = {}
    for r in rows:
        by_seed.setdefault(r["seed"], []).append(r)
    surv = [sum(x["simSeconds"] for x in v if x["diedA"]) / max(1, sum(1 for x in v if x["diedA"])) for v in by_seed.values()]
    kills_seed = [sum(x["enemiesKilled"] for x in v) / len(v) for v in by_seed.values()]
    died = [r for r in rows if r["diedA"]]
    wins = sum(1 for r in rows if not r["diedA"] and r["enemiesKilled"] == r["enemies"])
    return {
        "n": n,
        "died": 100 * len(died) / n,
        "wins": 100 * wins / n,
        "timeouts": sum(1 for r in rows if r.get("timedOut")),
        "ttd": sum(r["simSeconds"] for r in died) / len(died) if died else None,
        "kills": kills / n,
        "kills_rng": (min(kills_seed), max(kills_seed)),
        "taken_per_kill": sum(r["damageTakenA"] for r in rows) / kills if kills else None,
        "dealt_pm": 60 * sum(r["damageDealtA"] for r in rows) / sec,
        "taken_pm": 60 * sum(r["damageTakenA"] for r in rows) / sec,
        "bo": 100 * sum(r["breakOffStepsA"] for r in rows) / max(1, sum(r["stepsA"] for r in rows)),
        "regen": 100 * sum(r["regenStepsA"] for r in rows) / max(1, sum(r["stepsA"] for r in rows)),
        "ebo": 100 * sum(r["breakOffStepsEnemies"] for r in rows) / max(1, sum(r["stepsEnemies"] for r in rows)),
        "len": sec / n,
    }


def fmt(v, f="{:.0f}"):
    return "-" if v is None else f.format(v)


print("| Row | Ref wins | Ref died | Sec to die | Kills / fight (seed range) | Dmg taken / kill | Ref dealt / min | Ref taken / min | Ref break-off | Ref regen | Enemy break-off | Timeouts | Mean len |")
print("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|")
names = ["bridge-Crimson-x1-authored", "bridge-Valis-x1-authored"]
for enemy in ("Valis", "Crimson"):
    for n in (1, 2, 3):
        for bo in (0, 25, 50, 75):
            names.append(f"{enemy}-x{n}-bo{bo}")
for name in names:
    rows = load(name)
    if not rows:
        print(f"| {name} | MISSING |")
        continue
    s = stats(rows)
    print(f"| {name} | {s['wins']:.0f}% | {s['died']:.0f}% | {fmt(s['ttd'], '{:.1f}')} | {s['kills']:.2f} ({s['kills_rng'][0]:.2f}-{s['kills_rng'][1]:.2f}) | "
          f"{fmt(s['taken_per_kill'])} | {s['dealt_pm']:.0f} | {s['taken_pm']:.0f} | {s['bo']:.0f}% | {s['regen']:.0f}% | {s['ebo']:.0f}% | {s['timeouts']} | {s['len']:.1f} |")
a, b = load("Valis-x2-bo50"), load("repeat-Valis-x2-bo50")
if a and b:
    print("repeat identical:", sum(1 for x, y in zip(a, b) if x == y), "/", len(a))
for plain, over in (("bridge-Crimson-x1-authored", "Crimson-x1-bo25"), ("bridge-Valis-x1-authored", "Valis-x1-bo25")):
    a, b = load(plain), load(over)
    if a and b:
        print(plain, "vs", over, "identical:", sum(1 for x, y in zip(a, b) if x["simSeconds"] == y["simSeconds"] and x["damageTakenA"] == y["damageTakenA"]), "/", len(a))
