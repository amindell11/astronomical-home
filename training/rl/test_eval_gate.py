"""Unit tests for the eval gate's verdict rule (stdlib unittest; no pytest in this venv).

    cd training/rl
    .venv\\Scripts\\python -m unittest test_eval_gate -v
"""
import json
import tempfile
import unittest
from pathlib import Path

from checkpoint_watch import evaluated_summary
from eval_gate import (ALERT, CONTINUE, STOP, SUMMARY_SCHEMA, Score, degraded_reasons,
                       read_score, verdict)


def healthy(step: int) -> Score:
    return Score(step=step, evader_wins=13, total_wins=71)


def evader_bad(step: int) -> Score:
    return Score(step=step, evader_wins=9, total_wins=64)


def total_bad(step: int) -> Score:
    return Score(step=step, evader_wins=12, total_wins=52)


class VerdictRuleTests(unittest.TestCase):
    def test_no_checkpoints_yet_continues(self):
        self.assertEqual(CONTINUE, verdict([]))

    def test_seed_class_sequence_continues(self):
        self.assertEqual(CONTINUE, verdict([healthy(s) for s in (200_000, 400_000, 600_000)]))

    def test_single_bad_checkpoint_alerts_but_never_stops(self):
        self.assertEqual(ALERT, verdict([healthy(1), evader_bad(2)]))
        self.assertEqual(ALERT, verdict([evader_bad(1)]))

    def test_two_consecutive_bad_checkpoints_stop(self):
        self.assertEqual(STOP, verdict([healthy(1), evader_bad(2), evader_bad(3)]))

    def test_low_total_is_the_other_stop_path(self):
        self.assertEqual(ALERT, verdict([healthy(1), total_bad(2)]))
        self.assertEqual(STOP, verdict([healthy(1), total_bad(2), total_bad(3)]))

    def test_mixed_degradation_kinds_still_count_as_consecutive(self):
        self.assertEqual(STOP, verdict([evader_bad(1), total_bad(2)]))

    def test_recovery_resets_the_streak(self):
        self.assertEqual(CONTINUE, verdict([evader_bad(1), healthy(2)]))
        self.assertEqual(ALERT, verdict([evader_bad(1), healthy(2), evader_bad(3)]))

    def test_thresholds_are_inclusive_where_the_brief_says_so(self):
        self.assertEqual([], degraded_reasons(Score(1, evader_wins=11, total_wins=55)))
        self.assertEqual(1, len(degraded_reasons(Score(1, evader_wins=10, total_wins=55))))
        self.assertEqual(1, len(degraded_reasons(Score(1, evader_wins=11, total_wins=54))))
        self.assertEqual(2, len(degraded_reasons(Score(1, evader_wins=10, total_wins=54))))


class ReadScore(unittest.TestCase):
    """The gate's only summary consumer: reads the v2 opponents[] blocks and refuses anything else."""

    def setUp(self):
        self.path = Path(tempfile.mkdtemp()) / "20260729-120000-custom-summary.json"

    def write(self, payload) -> Path:
        self.path.write_text(json.dumps(payload))
        return self.path

    def test_reads_evader_and_total_wins_from_a_v2_summary(self):
        path = self.write({"schema": SUMMARY_SCHEMA, "opponents": [
            {"opponent": "Aggressor", "wins": 14},
            {"opponent": "Evader", "wins": 12},
            {"opponent": "Dummy", "wins": 15},
        ]})
        self.assertEqual(Score(step=200_000, evader_wins=12, total_wins=41), read_score(path, 200_000))

    def test_pre_v2_summary_fails_loud_instead_of_reading_zero_wins(self):
        path = self.write({"archetypes": [{"archetype": "Evader", "wins": 12}]})
        with self.assertRaises(SystemExit):
            read_score(path, 200_000)

    def test_missing_evader_block_fails(self):
        path = self.write({"schema": SUMMARY_SCHEMA, "opponents": [{"opponent": "Dummy", "wins": 15}]})
        with self.assertRaises(SystemExit):
            read_score(path, 200_000)


class EvaluatedSummary(unittest.TestCase):
    """A restarted gate must replay finished steps, not re-run them into the same directory."""

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())

    def test_unevaluated_step_runs(self):
        self.assertIsNone(evaluated_summary(self.dir))
        self.assertIsNone(evaluated_summary(self.dir / "never-created"))

    def test_finished_step_replays(self):
        (self.dir / "20260726-120000-gate-summary.json").write_text("{}")
        self.assertIsNotNone(evaluated_summary(self.dir))

    def test_ambiguous_dir_does_not_replay(self):
        (self.dir / "20260726-120000-gate-summary.json").write_text("{}")
        (self.dir / "20260726-130000-gate-summary.json").write_text("{}")
        self.assertIsNone(evaluated_summary(self.dir))


if __name__ == "__main__":
    unittest.main()
