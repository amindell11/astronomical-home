"""Writes the lethality-calibration crowd batches (2026-10-21). Usage: python mkcrowd.py <name>...

One stand-in (team 0) against N trial enemies (team 1). Draws depend on seed and episode only,
so every config with the same seed meets the same enemies in the same order.
"""
import json
import sys

FINAL = {"smartTrigger": True, "geomTolScale": 0.8, "ventTo": 0.0, "railAbortErr": 3.0, "aimLock": True,
         "aimAuthority": 10, "autoProfile": True, "rangeMissiles": 13, "matchupRange": True, "extendGunRange": True,
         "strafeFraction": 0.4, "strafePeriod": 1.2, "chargeTolScale": 1.2, "grenadeMinRange": 16,
         "rangeGrenades": 20, "railSteady": 0.3}
STRONG = dict(FINAL, label="stand-final")
WEAK = {"label": "stand-main"}
NOJINK = dict(FINAL, strafeFraction=0.0, label="stand-nojink")
HARD = dict(FINAL, strafeFraction=0.0, hardJink=True, strafePeriod=1.2, label="stand-hard")
STANDS = {"": STRONG, "-nojink": NOJINK, "-hard": HARD, "-weak": WEAK}

MAIN = {"label": "main"}
SERVO = {"aimLock": True, "label": "servo"}
DIAL = {"aimLock": True, "skillReactionMax": 0.5, "skillNoiseMax": 15, "skillAccelMin": 0.05, "skillCurve": 1.0,
        "skillTolGain": 3}


def dial(s, **over):
    d = dict(DIAL, skill=s, label=f"dial{s}")
    d.update(over)
    return d


def ea(e0, t, label=None, **over):
    d = {"aimLock": True, "eaE0": e0, "eaT": t, "eaCone": 30, "eaRange": 25, "eaJinkAccel": 5, "eaJinkReset": 1.0,
         "eaBoostReset": 1.0}
    d.update(over)
    d["label"] = label or f"ea{e0}-{t}"
    return d


def crowd(tag, enemy, stand=STRONG, ns=(1, 2, 3, 5), episodes=10, seed=9001, timeout=150, interval=4.0):
    return {"tag": tag, "mode": "crowd", "weapons": ["Lasers"], "crowd": list(ns), "episodes": episodes,
            "seed": seed, "timeoutSec": timeout, "spawnInterval": interval, "ringRadius": 55,
            "a": {"pilot": "AggressorPilot", "doctrine": json.dumps(stand)},
            "b": {"pilot": "", "doctrine": json.dumps(enemy)}}


def smoke():
    return [crowd("cs-main", MAIN, ns=(1, 3), episodes=2, timeout=60),
            crowd("cs-servo", SERVO, ns=(1, 3), episodes=2, timeout=60),
            crowd("cs-dial", dial(0.5), ns=(3,), episodes=2, timeout=60),
            crowd("cs-ea", ea(20, 4), ns=(3,), episodes=2, timeout=60)]


def r1():
    runs = [crowd("r-main", MAIN, episodes=20), crowd("r-servo", SERVO, episodes=20),
            crowd("r-main-weak", MAIN, stand=WEAK, episodes=20), crowd("r-servo-weak", SERVO, stand=WEAK, episodes=20)]
    runs += [crowd(f"d-{s}", dial(s), episodes=20) for s in (0.0, 0.25, 0.5, 0.75)]
    runs += [crowd(f"e-{e0}-{t}", ea(e0, t), episodes=20) for e0 in (10, 20, 30) for t in (3, 6)]
    return runs


def combo(s, e0, t, **over):
    d = dial(s)
    d.update(ea(e0, t))
    d.update(over)
    d["label"] = f"dial{s}+ea{e0}-{t}"
    return d


def r2():
    runs = []
    for suffix in ("-nojink", "-hard"):
        runs += [crowd(f"r-main{suffix}", MAIN, stand=STANDS[suffix], episodes=20),
                 crowd(f"r-servo{suffix}", SERVO, stand=STANDS[suffix], episodes=20)]
    for suffix in ("-nojink", "-hard", "-weak"):
        runs.append(crowd(f"d-0.25{suffix}", dial(0.25), stand=STANDS[suffix], episodes=20))
        runs.append(crowd(f"e-20-6{suffix}", ea(20, 6), stand=STANDS[suffix], episodes=20))
    runs.append(crowd("d-0.4", dial(0.4), episodes=20))
    runs.append(crowd("e-25-4", ea(25, 4), episodes=20))
    runs += [crowd("e-20-6-nojr", ea(20, 6, eaJinkAccel=-1), episodes=20),
             crowd("e-20-6-half", ea(20, 6, eaJinkReset=0.5), episodes=20),
             crowd("e-20-6-thr8", ea(20, 6, eaJinkAccel=8), episodes=20),
             crowd("e-20-6-norange", ea(20, 6, eaRange=-1), episodes=20),
             crowd("e-20-6-lose1", ea(20, 6, eaLoseRate=1), episodes=20)]
    runs += [crowd("c-0.5-10-6", combo(0.5, 10, 6), episodes=20),
             crowd("c-0.25-10-3", combo(0.25, 10, 3), episodes=20),
             crowd("c-0.75-20-6", combo(0.75, 20, 6), episodes=20)]
    return runs


BATCHES = {"smoke": smoke, "r1": r1, "r2": r2}

if __name__ == "__main__":
    for name in sys.argv[1:]:
        with open(f"crowd-{name}.json", "w") as f:
            json.dump({"runs": BATCHES[name]()}, f, indent=1)
        print(f"crowd-{name}.json")
