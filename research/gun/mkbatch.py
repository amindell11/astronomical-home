"""Writes the research-tail batch files (2026-10-20). Usage: python mkbatch.py"""
import json

ALL = ["Lasers", "TurboLasers", "Rippers", "ChargeLasers", "Railgun", "Missiles", "Grenades"]
GUNS = ["Lasers", "TurboLasers", "Rippers", "ChargeLasers", "Railgun"]
FINAL = {"smartTrigger": True, "geomTolScale": 0.8, "ventTo": 0.0, "railAbortErr": 3.0, "aimLock": True,
         "aimAuthority": 10, "autoProfile": True, "rangeMissiles": 13, "matchupRange": True, "extendGunRange": True,
         "strafeFraction": 0.4, "strafePeriod": 1.2, "chargeTolScale": 1.2, "grenadeMinRange": 16,
         "rangeGrenades": 20, "railSteady": 0.3}
LOCK_SMART_PROF = {"aimLock": True, "aimAuthority": 10, "autoProfile": True, "smartTrigger": True,
                   "geomTolScale": 0.8, "ventTo": 0.0, "railAbortErr": 3.0}
DODGE = {"strafeFraction": 0.4, "strafePeriod": 1.2, "label": "dodge"}


def doc(base, label, **over):
    d = dict(base)
    d.update(over)
    d["label"] = label
    return json.dumps(d)


def marks(tag, weapons, opponents, a, episodes=10, seed=8001, b=None, timeout=60):
    r = {"tag": tag, "mode": "marks", "weapons": weapons, "opponents": opponents, "episodes": episodes,
         "seed": seed, "timeoutSec": timeout, "a": {"pilot": "AggressorPilot", "doctrine": a}}
    if b:
        r["b"] = {"pilot": "ArchetypePilot", "doctrine": b}
    return r


def matrix(tag, weapons, a, b, opponents=None, episodes=20, seed=7001, ordered=False):
    r = {"tag": tag, "mode": "matrix", "weapons": weapons, "episodes": episodes, "seed": seed, "timeoutSec": 90,
         "a": {"pilot": "AggressorPilot", "doctrine": a}, "b": {"pilot": "AggressorPilot", "doctrine": b}}
    if opponents:
        r["opponents"] = opponents
    if ordered:
        r["ordered"] = True
    return r


def batch_a():
    runs = []
    t1 = [("t1-lock", {}),
          ("t1-quad10", {"aimLock": False}),
          ("t1-w01-a10", {"aimLock": False, "mpcFacingWidth": 0.1}),
          ("t1-w01-a30", {"aimLock": False, "mpcFacingWidth": 0.1, "aimAuthority": 30}),
          ("t1-w01-a30-yr0", {"aimLock": False, "mpcFacingWidth": 0.1, "aimAuthority": 30, "mpcYawRate": 0.0}),
          ("t1-w003-a100-yr0", {"aimLock": False, "mpcFacingWidth": 0.03, "aimAuthority": 100, "mpcYawRate": 0.0})]
    for tag, over in t1:
        runs.append(marks(tag, GUNS, ["Dummy", "Orbiter", "Aggressor"], doc(LOCK_SMART_PROF, tag, **over)))
    dodge = json.dumps(DODGE)
    for g, c in [(0.5, 0.8), (0.8, 1.2), (1.0, 1.6), (1.3, 2.0), (1.6, 2.5), (2.0, 3.0)]:
        tag = f"t3-g{g}-c{c}"
        runs.append(marks(tag, ["Rippers", "ChargeLasers"], ["Aggressor"],
                          doc(FINAL, tag, geomTolScale=g, chargeTolScale=c), episodes=20, seed=9001, b=dodge))
    for tap in (0.5, 0.67, 0.85):
        tag = f"t3-tap{tap}"
        runs.append(marks(tag, ["ChargeLasers"], ["Aggressor"], doc(FINAL, tag, tapCharge=tap),
                          episodes=20, seed=9001, b=dodge))
    for tag, over in [("t5-s1", {"skill": 1.0}),
                      ("t5-s0-A", {"skill": 0.0}),
                      ("t5-s0-B", {"skill": 0.0, "skillReactionMax": 0.5, "skillNoiseMax": 15, "skillAccelMin": 0.05}),
                      ("t5-s0-C", {"skill": 0.0, "skillReactionMax": 0.2, "skillNoiseMax": 6, "skillAccelMin": 0.2}),
                      ("t5-s05-A", {"skill": 0.5})]:
        runs.append(marks(tag, ALL, ["Aggressor"], doc(FINAL, tag, **over)))
    for tag, over in [("t2-ref", {}),
                      ("t2-safe2", {"grenadeSafeMargin": 2}),
                      ("t2-safe4", {"grenadeSafeMargin": 4}),
                      ("t2-back", {"grenadeBackoff": True}),
                      ("t2-safe2-back", {"grenadeSafeMargin": 2, "grenadeBackoff": True})]:
        runs.append(marks(tag, ["Grenades"], ["Dummy", "Orbiter", "Aggressor"], doc(FINAL, tag, **over)))
    for tag, over in [("t4-ref", {}),
                      ("t4-orbit", {"railOrbit": True}),
                      ("t4-orbit-c6", {"railOrbit": True, "railOrbitCommit": 6}),
                      ("t4-orbit-r150", {"railOrbit": True, "railOrbitRate": 150})]:
        runs.append(marks(tag, ["Railgun"], ["Orbiter", "Aggressor", "Dummy"], doc(FINAL, tag, **over),
                          episodes=20, seed=9101))
    final = doc(FINAL, "final")
    for tag, over in [("t2x-ref", {}),
                      ("t2x-safe2", {"grenadeSafeMargin": 2}),
                      ("t2x-safe2-back", {"grenadeSafeMargin": 2, "grenadeBackoff": True})]:
        runs.append(matrix(tag, ["Grenades"], doc(FINAL, tag, **over), final, opponents=ALL))
    runs.append(matrix("t6-rail15", ["Railgun"], final, final, opponents=ALL, episodes=40))
    return runs


if __name__ == "__main__":
    import sys
    which = sys.argv[1] if len(sys.argv) > 1 else "a"
    runs = {"a": batch_a}[which]()
    with open(f"tail-{which}.json", "w") as f:
        json.dump({"runs": runs}, f, indent=1)
    print(len(runs), "runs")
