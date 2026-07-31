"""Unit tests for the eval gate's verdict layer (stdlib unittest; no pytest in this venv).

    cd training/rl
    .venv\\Scripts\\python -m unittest test_eval_gate -v
"""
import json
import tempfile
import unittest
from pathlib import Path

from checkpoint_watch import PendingStep, discover_reps, rep_dir
from eval_bundle import load_bundle
from eval_gate import (ALERT, CONTINUE, STOP, SUMMARY_SCHEMA, CheckpointRead, GateState, Score,
                       bundle_protocol, degraded_reasons, judge_checkpoint, pool_scores,
                       process_step, read_score, verdict)

# Loading the committed v1 file here doubles as a pin test on its frozen values.
BUNDLE = load_bundle()
THRESHOLDS = BUNDLE.thresholds

ARCHETYPES = ("Aggressor", "Evader", "Orbiter", "Kiter", "Dummy")


def score(step, evader_wins, total_wins, evader_episodes=15, total_episodes=75):
    return Score(step, evader_wins, evader_episodes, total_wins, total_episodes)


def reasons(s):
    return degraded_reasons(s, THRESHOLDS)


def clear_read(step):
    return CheckpointRead(step, score(step, 13, 71), reps=1, degraded=False, confirmed=False)


def unconfirmed_read(step):
    return CheckpointRead(step, score(step, 9, 64), reps=1, degraded=True, confirmed=False)


def confirmed_read(step):
    return CheckpointRead(step, score(step, 27, 192, evader_episodes=45, total_episodes=225),
                          reps=3, degraded=True, confirmed=True)


def write_summary(directory: Path, evader_wins: int, other_wins: int = 14,
                  episodes: int = 15) -> Path:
    directory.mkdir(parents=True, exist_ok=True)
    blocks = [{"opponent": a, "wins": evader_wins if a == "Evader" else other_wins,
               "episodes": episodes} for a in ARCHETYPES]
    path = directory / "20260731-000000-custom-summary.json"
    path.write_text(json.dumps({"schema": SUMMARY_SCHEMA, "opponents": blocks}))
    return path


class VerdictRuleTests(unittest.TestCase):
    def test_no_checkpoints_yet_continues(self):
        self.assertEqual(CONTINUE, verdict([]))

    def test_seed_class_sequence_continues(self):
        self.assertEqual(CONTINUE, verdict([clear_read(s) for s in (200_000, 400_000, 600_000)]))

    def test_unconfirmed_degradation_never_alerts(self):
        self.assertEqual(CONTINUE, verdict([unconfirmed_read(1)]))
        self.assertEqual(CONTINUE, verdict([clear_read(1), unconfirmed_read(2)]))

    def test_single_confirmed_checkpoint_alerts_but_never_stops(self):
        self.assertEqual(ALERT, verdict([confirmed_read(1)]))
        self.assertEqual(ALERT, verdict([clear_read(1), confirmed_read(2)]))

    def test_two_consecutive_confirmed_checkpoints_stop(self):
        self.assertEqual(STOP, verdict([clear_read(1), confirmed_read(2), confirmed_read(3)]))

    def test_recovery_resets_the_streak(self):
        self.assertEqual(CONTINUE, verdict([confirmed_read(1), clear_read(2)]))
        self.assertEqual(ALERT, verdict([confirmed_read(1), clear_read(2), confirmed_read(3)]))


class DegradedPredicate(unittest.TestCase):
    def test_thresholds_are_inclusive_where_the_brief_says_so(self):
        self.assertEqual([], reasons(score(1, evader_wins=11, total_wins=55)))
        self.assertEqual(1, len(reasons(score(1, evader_wins=10, total_wins=55))))
        self.assertEqual(1, len(reasons(score(1, evader_wins=11, total_wins=54))))
        self.assertEqual(2, len(reasons(score(1, evader_wins=10, total_wins=54))))

    def test_pooled_cells_are_judged_at_the_same_rates(self):
        # 30/45 = 10/15 exactly; 165/225 = 55/75 exactly.
        at_evader_rate = score(1, 30, 165, evader_episodes=45, total_episodes=225)
        self.assertEqual(1, len(reasons(at_evader_rate)))
        above_both = score(1, 31, 165, evader_episodes=45, total_episodes=225)
        self.assertEqual([], reasons(above_both))
        below_total = score(1, 31, 164, evader_episodes=45, total_episodes=225)
        self.assertEqual(1, len(reasons(below_total)))

    def test_reasons_quote_the_wilson_interval(self):
        for reason in reasons(score(1, evader_wins=9, total_wins=52)):
            self.assertIn("Wilson95", reason)

    def test_pool_scores_sums_replicate_cells(self):
        pooled = pool_scores(7, [score(7, 9, 64), score(7, 11, 70), score(7, 10, 66)])
        self.assertEqual(Score(7, 30, 45, 200, 225), pooled)


class BundleProtocol(unittest.TestCase):
    """The bundle is the seed authority; the legacy flags may only restate its protocol."""

    def test_defaults_come_from_the_bundle(self):
        self.assertEqual(("2001,2002,2003,2004,2005", 3), bundle_protocol(BUNDLE))

    def test_restating_the_bundle_is_allowed_in_any_order(self):
        seeds, eps = bundle_protocol(BUNDLE, "2005,2001,2002,2003,2004", 3)
        self.assertEqual("2001,2002,2003,2004,2005", seeds)
        self.assertEqual(3, eps)

    def test_off_bundle_seeds_fail_loud(self):
        with self.assertRaises(SystemExit):
            bundle_protocol(BUNDLE, "3001,3002,3003,3004,3005", None)

    def test_off_bundle_episodes_per_seed_fails_loud(self):
        with self.assertRaises(SystemExit):
            bundle_protocol(BUNDLE, None, 5)

    def test_garbage_seed_list_fails_loud(self):
        with self.assertRaises(SystemExit):
            bundle_protocol(BUNDLE, "2001,evader,2003", None)


class ReadScore(unittest.TestCase):
    """The gate's only summary consumer: reads the v2 opponents[] blocks and refuses anything else."""

    def setUp(self):
        self.path = Path(tempfile.mkdtemp()) / "20260729-120000-custom-summary.json"

    def write(self, payload) -> Path:
        self.path.write_text(json.dumps(payload))
        return self.path

    def test_reads_cells_from_a_v2_summary(self):
        path = self.write({"schema": SUMMARY_SCHEMA, "opponents": [
            {"opponent": "Aggressor", "wins": 14, "episodes": 15},
            {"opponent": "Evader", "wins": 12, "episodes": 15},
            {"opponent": "Dummy", "wins": 15, "episodes": 15},
        ]})
        self.assertEqual(Score(step=200_000, evader_wins=12, evader_episodes=15,
                               total_wins=41, total_episodes=45),
                         read_score(path, 200_000))

    def test_pre_v2_summary_fails_loud_instead_of_reading_zero_wins(self):
        path = self.write({"archetypes": [{"archetype": "Evader", "wins": 12}]})
        with self.assertRaises(SystemExit):
            read_score(path, 200_000)

    def test_missing_evader_block_fails(self):
        path = self.write({"schema": SUMMARY_SCHEMA,
                           "opponents": [{"opponent": "Dummy", "wins": 15, "episodes": 15}]})
        with self.assertRaises(SystemExit):
            read_score(path, 200_000)


class DiscoverReps(unittest.TestCase):
    """A restarted gate replays finished replicates; layouts that would corrupt pooling refuse."""

    def setUp(self):
        self.step_dir = Path(tempfile.mkdtemp()) / "step-200000"

    def test_missing_step_dir_has_no_reps(self):
        self.assertEqual((), discover_reps(self.step_dir))

    def test_finished_reps_are_listed_ascending(self):
        write_summary(rep_dir(self.step_dir, 2), evader_wins=10)
        one = write_summary(rep_dir(self.step_dir, 1), evader_wins=13)
        reps = discover_reps(self.step_dir)
        self.assertEqual([1, 2], [k for k, _ in reps])
        self.assertEqual(one, reps[0][1])

    def test_unfinished_rep_dir_is_not_a_sample(self):
        rep_dir(self.step_dir, 1).mkdir(parents=True)
        self.assertEqual((), discover_reps(self.step_dir))

    def test_root_level_summary_is_a_pre_replicate_layout_and_refuses(self):
        write_summary(self.step_dir, evader_wins=13)
        with self.assertRaises(SystemExit):
            discover_reps(self.step_dir)

    def test_two_summaries_in_one_rep_dir_refuse(self):
        d = rep_dir(self.step_dir, 1)
        write_summary(d, evader_wins=13)
        (d / "20260731-010000-custom-summary.json").write_text("{}")
        with self.assertRaises(SystemExit):
            discover_reps(self.step_dir)


class FakeLane:
    """Stands in for the lane launcher: writes a synthetic summary per requested replicate."""

    def __init__(self, evader_by_rep):
        self.evader_by_rep = evader_by_rep
        self.calls = []

    def __call__(self, out_dir, k):
        self.calls.append(k)
        return write_summary(out_dir, self.evader_by_rep[k])


def pending_step(step_dir: Path, step: int = 200_000) -> PendingStep:
    return PendingStep(step, Path(f"ShipCombat-{step}.onnx"), step_dir, discover_reps(step_dir))


class JudgeCheckpoint(unittest.TestCase):
    """The adaptive-confirmation protocol: watch replicate, confirmation only while armed
    and degraded-unconfirmed, pooled re-verdict, replay from whatever is on disk."""

    def setUp(self):
        self.step_dir = Path(tempfile.mkdtemp()) / "step-200000"

    def judge(self, lane, armed):
        return judge_checkpoint(pending_step(self.step_dir), BUNDLE, armed, lane)

    def test_healthy_watch_read_runs_one_replicate(self):
        lane = FakeLane({1: 13})
        reps, _, pooled, degraded, confirmed = self.judge(lane, armed=True)
        self.assertEqual([1], lane.calls)
        self.assertEqual(1, len(reps))
        self.assertFalse(degraded)
        self.assertFalse(confirmed)
        self.assertEqual(15, pooled.evader_episodes)

    def test_unarmed_degraded_read_gets_no_confirmation(self):
        lane = FakeLane({1: 9})
        reps, _, _, degraded, confirmed = self.judge(lane, armed=False)
        self.assertEqual([1], lane.calls)
        self.assertTrue(degraded)
        self.assertFalse(confirmed)

    def test_armed_degraded_read_confirms_over_pooled_cells(self):
        lane = FakeLane({1: 9, 2: 9, 3: 9})
        reps, _, pooled, degraded, confirmed = self.judge(lane, armed=True)
        self.assertEqual([1, 2, 3], lane.calls)
        self.assertEqual(45, pooled.evader_episodes)
        self.assertEqual(27, pooled.evader_wins)
        self.assertTrue(degraded)
        self.assertTrue(confirmed)

    def test_confirmation_stops_early_when_the_pool_turns_healthy(self):
        lane = FakeLane({1: 8, 2: 15})
        reps, _, pooled, degraded, confirmed = self.judge(lane, armed=True)
        self.assertEqual([1, 2], lane.calls)
        self.assertEqual(23, pooled.evader_wins)
        self.assertFalse(degraded)
        self.assertFalse(confirmed)

    def test_replay_of_a_confirmed_step_spends_no_evals(self):
        for k in (1, 2, 3):
            write_summary(rep_dir(self.step_dir, k), evader_wins=9)
        lane = FakeLane({})
        reps, _, _, degraded, confirmed = self.judge(lane, armed=True)
        self.assertEqual([], lane.calls)
        self.assertEqual(3, len(reps))
        self.assertTrue(confirmed)

    def test_replay_runs_only_the_missing_confirmation_replicate(self):
        write_summary(rep_dir(self.step_dir, 1), evader_wins=9)
        write_summary(rep_dir(self.step_dir, 2), evader_wins=10)
        lane = FakeLane({3: 9})
        reps, _, pooled, _, confirmed = self.judge(lane, armed=True)
        self.assertEqual([3], lane.calls)
        self.assertEqual(45, pooled.evader_episodes)
        self.assertTrue(confirmed)

    def test_replay_of_a_partial_healthy_pool_runs_nothing(self):
        write_summary(rep_dir(self.step_dir, 1), evader_wins=9)
        write_summary(rep_dir(self.step_dir, 2), evader_wins=15)
        lane = FakeLane({})
        reps, _, _, degraded, confirmed = self.judge(lane, armed=True)
        self.assertEqual([], lane.calls)
        self.assertEqual(2, len(reps))
        self.assertFalse(degraded)
        self.assertFalse(confirmed)


class ProcessStepFlow(unittest.TestCase):
    """The gate state machine end to end: unarmed -> armed -> confirmed ALERT -> streak STOP."""

    def setUp(self):
        self.root = Path(tempfile.mkdtemp())
        self.state = GateState()

    def step(self, step, evader_by_rep):
        lane = FakeLane(evader_by_rep)
        pending = pending_step(self.root / f"step-{step}", step)
        current, _, _, payload = process_step(pending, BUNDLE, self.state, lane, "run")
        return current, payload

    def test_gate_arms_on_first_healthy_checkpoint_and_then_enforces(self):
        current, payload = self.step(100, {1: 8})
        self.assertEqual(CONTINUE, current)
        self.assertFalse(payload["armed"])
        self.assertIsNone(payload["armingStep"])
        self.assertTrue(payload["degraded"])

        current, payload = self.step(200, {1: 13})
        self.assertEqual(CONTINUE, current)
        self.assertTrue(payload["armed"])
        self.assertEqual(200, payload["armingStep"])

        current, payload = self.step(300, {1: 9, 2: 9, 3: 10})
        self.assertEqual(ALERT, current)
        self.assertTrue(payload["confirmed"])
        self.assertEqual(3, len(payload["evidence"]["reps"]))

        current, payload = self.step(400, {1: 9, 2: 9, 3: 9})
        self.assertEqual(STOP, current)

    def test_exoneration_resets_nothing_because_it_never_confirmed(self):
        self.step(100, {1: 13})
        current, payload = self.step(200, {1: 9, 2: 15})
        self.assertEqual(CONTINUE, current)
        self.assertFalse(payload["confirmed"])
        current, _ = self.step(300, {1: 9, 2: 9, 3: 9})
        self.assertEqual(ALERT, current, "an exonerated checkpoint must not count toward the streak")

    def test_payload_carries_bundle_id_and_provenance(self):
        _, payload = self.step(100, {1: 13})
        self.assertEqual(BUNDLE.bundle_id, payload["bundleId"])
        self.assertEqual(1, payload["evidence"]["reps"][0]["rep"])
        self.assertIn("summary", payload["evidence"]["reps"][0])
        self.assertEqual(2, len(payload["evidence"]["reps"][0]["summary"].split("step-100")),
                         "each sample's provenance names its own run dir")


if __name__ == "__main__":
    unittest.main()
