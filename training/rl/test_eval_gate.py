"""Unit tests for the eval gate's verdict rule (stdlib unittest; no pytest in this venv).

    cd training/rl
    .venv\\Scripts\\python -m unittest test_eval_gate -v
"""
import tempfile
import unittest
from pathlib import Path

from eval_gate import ALERT, CONTINUE, STOP, Score, degraded_reasons, evaluated_summary, verdict


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
