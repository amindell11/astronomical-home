"""Calibration bundle: one versioned config carrying everything that makes a gate verdict
comparable — seed set, episodes/seed, thresholds, arming predicate, replicate counts
(K_watch / K_confirm / K_bank), and execution mode (jobs-off is tighter but SHIFTED, so
the mode is part of the measurement). Every verdict artifact records the bundle id that
judged it.

v1 is today's protocol verbatim. Threshold recalibration is locked to the combat rules
change and arrives as bundle v2 — a new file, never an edit to v1.
"""
import json
import sys
from pathlib import Path
from typing import NamedTuple

BUNDLE_V1 = Path(__file__).resolve().parent / "eval_bundle_v1.json"
# v1's only arming predicate: the first checkpoint whose read is not degraded arms the gate.
ARMING_HEALTHY = "healthy"


class Thresholds(NamedTuple):
    alert_evader_wins: int
    evader_episodes: int
    min_total_wins: int
    total_episodes: int


class Bundle(NamedTuple):
    bundle_id: str
    seeds: tuple
    episodes_per_seed: int
    thresholds: Thresholds
    arming_predicate: str
    k_watch: int
    k_confirm: int
    k_bank: int
    execution_mode: str


def load_bundle(path=BUNDLE_V1) -> Bundle:
    """Parse-don't-validate boundary for the bundle file; everything downstream trusts the result."""
    path = Path(path)

    def fail(why):
        sys.exit(f"FAIL: calibration bundle {path}: {why}")

    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        fail("file not found")
    except json.JSONDecodeError as e:
        fail(f"not valid JSON ({e})")

    def field(source, name, kind, where=""):
        if name not in source:
            fail(f"missing {where}'{name}'")
        value = source[name]
        if isinstance(value, bool) or not isinstance(value, kind):
            fail(f"{where}'{name}' must be {kind.__name__}, got {value!r}")
        return value

    bundle_id = field(raw, "bundleId", str)
    if not bundle_id:
        fail("'bundleId' is empty")
    seeds = field(raw, "seeds", list)
    if not seeds or any(isinstance(s, bool) or not isinstance(s, int) for s in seeds):
        fail("'seeds' must be a non-empty list of ints")
    if len(set(seeds)) != len(seeds):
        fail("'seeds' has duplicates")
    episodes_per_seed = field(raw, "episodesPerSeed", int)
    if episodes_per_seed < 1:
        fail("'episodesPerSeed' must be >= 1")

    t = field(raw, "thresholds", dict)
    thresholds = Thresholds(*(field(t, name, int, where="thresholds.") for name in
                              ("alertEvaderWins", "evaderEpisodes", "minTotalWins", "totalEpisodes")))
    if not 0 <= thresholds.alert_evader_wins < thresholds.evader_episodes:
        fail(f"alertEvaderWins {thresholds.alert_evader_wins} outside 0..{thresholds.evader_episodes - 1}")
    if not 0 < thresholds.min_total_wins <= thresholds.total_episodes:
        fail(f"minTotalWins {thresholds.min_total_wins} outside 1..{thresholds.total_episodes}")
    # The old CLI hardcode (seeds x eps = 15), now owned by the bundle whose thresholds it feeds.
    if len(seeds) * episodes_per_seed != thresholds.evader_episodes:
        fail(f"seeds x episodesPerSeed = {len(seeds) * episodes_per_seed} but "
             f"thresholds.evaderEpisodes = {thresholds.evader_episodes}")
    if thresholds.total_episodes % thresholds.evader_episodes != 0:
        fail(f"totalEpisodes {thresholds.total_episodes} is not a whole number of "
             f"{thresholds.evader_episodes}-episode per-opponent blocks")

    arming_predicate = field(raw, "armingPredicate", str)
    if arming_predicate != ARMING_HEALTHY:
        fail(f"unknown armingPredicate {arming_predicate!r}; v1 knows only {ARMING_HEALTHY!r}")
    k_watch = field(raw, "kWatch", int)
    if k_watch < 1:
        fail("'kWatch' must be >= 1")
    k_confirm = field(raw, "kConfirm", int)
    if k_confirm < 1:
        fail("'kConfirm' must be >= 1")
    k_bank = field(raw, "kBank", int)
    if k_bank < 2:
        fail("'kBank' must be >= 2 (banking pools replicates)")
    execution_mode = field(raw, "executionMode", str)
    if not execution_mode:
        fail("'executionMode' is empty")

    return Bundle(bundle_id, tuple(seeds), episodes_per_seed, thresholds, arming_predicate,
                  k_watch, k_confirm, k_bank, execution_mode)
