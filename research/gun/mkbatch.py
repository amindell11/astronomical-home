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


DIAL = {"skillReactionMax": 0.5, "skillNoiseMax": 15, "skillAccelMin": 0.05, "skillCurve": 0.5}


def batch_b():
    runs = []
    mpc = {"aimLock": False, "mpcFacingWidth": 0.1, "aimAuthority": 30}
    for tag, over in [("t1b-n025-w01-a30", dict(mpc, mpcNoiseStd=0.25)),
                      ("t1b-n025-quad10", {"aimLock": False, "mpcNoiseStd": 0.25}),
                      ("t1b-n01-w01-a30", dict(mpc, mpcNoiseStd=0.1)),
                      ("t1b-s512-w01-a30", dict(mpc, mpcSamples=512)),
                      ("t1b-n025-s512-w01-a30", dict(mpc, mpcNoiseStd=0.25, mpcSamples=512))]:
        runs.append(marks(tag, GUNS, ["Dummy", "Orbiter", "Aggressor"], doc(LOCK_SMART_PROF, tag, **over)))
    final = doc(FINAL, "final")
    for g, c in [(0.5, 0.8), (0.8, 1.2), (1.2, 1.8), (1.6, 2.5), (2.5, 3.5)]:
        tag = f"t3x-g{g}-c{c}"
        runs.append(matrix(tag, ["Rippers", "ChargeLasers"], doc(FINAL, tag, geomTolScale=g, chargeTolScale=c), final,
                           opponents=GUNS))
    for tap in (0.31, 0.5):
        tag = f"t3x-tap{tap}"
        runs.append(matrix(tag, ["ChargeLasers"], doc(FINAL, tag, tapCharge=tap), final, opponents=GUNS))
    for tag, over in [("t2b-chain", {"grenadeSafeMargin": 2, "grenadeBackoff": True, "grenadeChainGap": 0.2}),
                      ("t2b-chain-only", {"grenadeChainGap": 0.2}),
                      ("t2b-chain-back", {"grenadeBackoff": True, "grenadeChainGap": 0.2})]:
        runs.append(marks(tag, ["Grenades"], ["Dummy", "Orbiter", "Aggressor"], doc(FINAL, tag, **over)))
    for tag, over in [("t2bx-ref", {}),
                      ("t2bx-safe2-back", {"grenadeSafeMargin": 2, "grenadeBackoff": True}),
                      ("t2bx-chain", {"grenadeSafeMargin": 2, "grenadeBackoff": True, "grenadeChainGap": 0.2})]:
        runs.append(matrix(tag, ["Grenades"], doc(FINAL, tag, **over), final, opponents=["Missiles", "Grenades"]))
    runs.append(marks("t4-orbit-r45-c20", ["Railgun"], ["Orbiter", "Aggressor", "Dummy"],
                      doc(FINAL, "t4-orbit-r45-c20", railOrbit=True, railOrbitRate=45, railOrbitCommit=20),
                      episodes=20, seed=9101))
    base = json.dumps({"label": "base"})
    for s in (0.0, 0.25, 0.5, 0.75, 1.0):
        dial = doc(FINAL, f"dial-s{s}", skill=s, **DIAL)
        runs.append(marks(f"t5d-s{s}", ALL, ["Aggressor"], dial))
        for w in GUNS:
            runs.append(matrix(f"t5m-s{s}-{w}", [w], dial, final, opponents=[w], seed=7101))
            runs.append(matrix(f"t5b-s{s}-{w}", [w], dial, base, opponents=[w], seed=7201))
    return runs


def batch_c():
    runs = []
    mpc = {"aimLock": False, "mpcFacingWidth": 0.1, "aimAuthority": 30, "mpcNoiseStd": 0.1}
    mpc_final = doc(FINAL, "final-mpcaim", **mpc)
    runs.append(matrix("t1x-mpc", ALL, mpc_final, mpc_final))
    final = doc(FINAL, "final")
    for w in GUNS:
        runs.append(matrix(f"t1x-cross-{w}", [w], mpc_final, final, opponents=[w], seed=7401))
    return runs


def batch_d():
    final = doc(FINAL, "final")
    return [matrix("t6-x9-c11", ALL, final, final),
            matrix("t6-rail11", ["Railgun"], final, final, opponents=ALL, episodes=40),
            marks("t6-marks11", ["Railgun"], ["Orbiter", "Aggressor", "Dummy"], doc(FINAL, "t6-marks11"),
                  episodes=20, seed=9101)]


DIAL2 = {"skillReactionMax": 0.5, "skillNoiseMax": 8, "skillAccelMin": 0.05, "skillCurve": 0.5, "skillTolGain": 3}


def batch_e():
    runs = []
    base = json.dumps({"label": "base"})
    mid = doc(FINAL, "dial2-s0.5", skill=0.5, **DIAL2)
    for s in (0.0, 0.25, 0.5, 0.75, 1.0):
        dial = doc(FINAL, f"dial2-s{s}", skill=s, **DIAL2)
        runs.append(marks(f"t5e-s{s}", ALL, ["Aggressor"], dial))
        for w in GUNS:
            runs.append(matrix(f"t5eb-s{s}-{w}", [w], dial, base, opponents=[w], seed=7201))
            runs.append(matrix(f"t5em-s{s}-{w}", [w], dial, mid, opponents=[w], seed=7501))
    return runs



def dial3(noise, reaction):
    return {"skillReactionMax": reaction, "skillNoiseMax": noise, "skillAccelMin": 0.05, "skillCurve": 0.5,
            "skillTolGain": 3}


def batch_f():
    runs = []
    for name, noise, reaction in (("v3a", 15, 0.5), ("v3b", 20, 0.6), ("v3c", 25, 0.7)):
        for s in (0.0, 0.25, 0.5, 0.75):
            runs.append(marks(f"t5f-{name}-s{s}", ALL, ["Aggressor"],
                              doc(FINAL, f"dial-{name}-s{s}", skill=s, **dial3(noise, reaction))))
    return runs


if __name__ == "__main__":
    import sys
    which = sys.argv[1] if len(sys.argv) > 1 else "a"
    runs = {"a": batch_a, "b": batch_b, "c": batch_c, "d": batch_d, "e": batch_e, "f": batch_f}[which]()
    with open(f"tail-{which}.json", "w") as f:
        json.dump({"runs": runs}, f, indent=1)
    print(len(runs), "runs")
