"""Unit tests for the bench driver's aggregation core and protocol orchestration
(stdlib unittest; no pytest in this venv).

    cd training/rl
    .venv\\Scripts\\python -m unittest test_bench_replicates -v
"""
import unittest
from pathlib import Path
from unittest import mock

import bench_replicates


def summary_of(cells: dict) -> dict:
    return {"opponents": [{"opponent": opp, "episodes": wins + losses + draws,
                           "wins": wins, "losses": losses, "draws": draws}
                          for opp, (wins, losses, draws) in cells.items()]}


def episodes_of(cells: dict, timeouts: dict = {}) -> list:
    """One row per episode, opponent labeled the rl-episode-v5 way (an object carrying
    `archetype`); the first `timeouts[opp]` draws of an opponent time out."""
    rows = []
    for opp, (wins, losses, draws) in cells.items():
        label = {"archetype": opp, "speedFraction": 0.9}
        rows += [{"opponent": label, "outcome": "Win", "timedOut": False}] * wins
        rows += [{"opponent": label, "outcome": "Loss", "timedOut": False}] * losses
        for i in range(draws):
            rows.append({"opponent": label, "outcome": "Draw", "timedOut": i < timeouts.get(opp, 0)})
    return rows


def read_of(name: str, cells: dict, timeouts: dict = {}, probes: dict = {}) -> dict:
    return {"dir": name, "summary": f"{name}/x-summary.json",
            "cells": bench_replicates.session_cells(summary_of(cells), episodes_of(cells, timeouts)),
            "probes": probes}


class SessionCells(unittest.TestCase):

    def test_counts_wins_draws_and_jsonl_only_timeouts_per_opponent(self):
        cells = bench_replicates.session_cells(
            summary_of({"Dummy": (15, 0, 0), "Evader": (12, 2, 1)}),
            episodes_of({"Dummy": (15, 0, 0), "Evader": (12, 2, 1)}, timeouts={"Evader": 1}))

        self.assertEqual({"episodes": 15, "wins": 15, "losses": 0, "draws": 0, "timeouts": 0},
                         cells["Dummy"])
        self.assertEqual({"episodes": 15, "wins": 12, "losses": 2, "draws": 1, "timeouts": 1},
                         cells["Evader"])

    def test_episode_row_with_unknown_opponent_fails_loud(self):
        with self.assertRaisesRegex(ValueError, "absent from the summary"):
            bench_replicates.session_cells(
                summary_of({"Dummy": (1, 0, 0)}),
                [{"opponent": {"archetype": "Ghost"}, "outcome": "Win", "timedOut": False}])

    def test_summary_vs_jsonl_win_disagreement_fails_loud(self):
        with self.assertRaisesRegex(ValueError, "episode\\s+JSONL"):
            bench_replicates.session_cells(summary_of({"Dummy": (2, 0, 0)}),
                                           episodes_of({"Dummy": (1, 1, 0)}))


class TabulateProbes(unittest.TestCase):

    def test_means_numeric_fields_per_probe_per_opponent(self):
        reps = [{"facing": {"opponents": [
                    {"schema": "rl-facing-probe-summary-v1", "opponent": "Kiter",
                     "episodes": 15, "meanFacingErrorDeg": 40.0}]}},
                {"facing": {"opponents": [
                    {"schema": "rl-facing-probe-summary-v1", "opponent": "Kiter",
                     "episodes": 15, "meanFacingErrorDeg": 50.0}]}}]

        tabulated = bench_replicates.tabulate_probes(reps)

        self.assertEqual({"facing": {"Kiter": {"episodes": 15.0, "meanFacingErrorDeg": 45.0}}},
                         tabulated)

    def test_probe_names_are_opaque_so_a_future_probe_needs_no_code_change(self):
        reps = [{"controller": {"opponents": [{"opponent": "Dummy", "meanYawTorque": 0.5}]}}]

        self.assertEqual({"controller": {"Dummy": {"meanYawTorque": 0.5}}},
                         bench_replicates.tabulate_probes(reps))

    def test_sessions_disagreeing_on_probe_set_fail_loud(self):
        with self.assertRaisesRegex(ValueError, "disagree"):
            bench_replicates.tabulate_probes([{"facing": {"opponents": []}}, {}])

    def test_no_probes_tabulates_empty(self):
        self.assertEqual({}, bench_replicates.tabulate_probes([{}, {}]))


class BuildAggregate(unittest.TestCase):

    ROSTER = {"Aggressor": (14, 1, 0), "Dummy": (15, 0, 0), "Evader": (12, 2, 1)}
    META = dict(run_name="bench-test",
                checkpoint={"path": "c.onnx", "md5": "d41d8", "project": "proj", "projectHead": "abc"},
                protocol={"seeds": "2001", "episodesPerSeed": 3, "density": 2.0,
                          "replicates": 2, "probes": "facing", "mirror": True})

    def two_rep_aggregate(self):
        reps = [read_of("rep-01", self.ROSTER, timeouts={"Evader": 1}),
                read_of("rep-02", {"Aggressor": (13, 2, 0), "Dummy": (15, 0, 0),
                                   "Evader": (14, 1, 0)})]
        mirror = read_of("mirror", {"Mirror": (7, 7, 1)}, timeouts={"Mirror": 1})
        return bench_replicates.build_aggregate(rep_reads=reps, mirror_read=mirror, **self.META)

    def test_totals_mean_stdev_and_spread_across_replicates(self):
        totals = self.two_rep_aggregate()["totals"]

        self.assertEqual([41, 42], totals["perRep"])
        self.assertEqual(45, totals["episodesPerRep"])
        self.assertAlmostEqual(41.5, totals["mean"])
        self.assertAlmostEqual(0.7071, totals["stdev"], places=4)
        self.assertEqual(1, totals["spread"])

    def test_per_opponent_draw_and_timeout_counts_are_surfaced(self):
        evader = self.two_rep_aggregate()["perOpponent"]["Evader"]

        self.assertEqual(1, evader["draws"])
        self.assertEqual([1, 0], evader["drawsPerRep"])
        self.assertEqual(1, evader["timeouts"])
        self.assertEqual([1, 0], evader["timeoutsPerRep"])
        self.assertAlmostEqual(13.0, evader["meanWins"])

    def test_mirror_cells_ride_along(self):
        mirror = self.two_rep_aggregate()["mirror"]

        self.assertEqual("mirror", mirror["dir"])
        self.assertEqual(1, mirror["opponents"]["Mirror"]["draws"])
        self.assertEqual(1, mirror["opponents"]["Mirror"]["timeouts"])

    def test_single_replicate_has_no_stdev_or_sem(self):
        agg = bench_replicates.build_aggregate(
            rep_reads=[read_of("rep-01", self.ROSTER)], mirror_read=None, **self.META)

        self.assertIsNone(agg["totals"]["stdev"])
        self.assertIsNone(agg["totals"]["sem"])
        self.assertIsNone(agg["perOpponent"]["Evader"]["stdevWins"])
        self.assertIsNone(agg["mirror"])

    def test_replicates_running_different_opponent_sets_fail_loud(self):
        with self.assertRaisesRegex(ValueError, "opponents"):
            bench_replicates.build_aggregate(
                rep_reads=[read_of("rep-01", self.ROSTER), read_of("rep-02", {"Dummy": (15, 0, 0)})],
                mirror_read=None, **self.META)


class RenderMarkdown(unittest.TestCase):

    def test_renders_rep_table_mean_line_draw_table_and_mirror(self):
        agg = BuildAggregate().two_rep_aggregate()

        md = bench_replicates.render_markdown(agg)

        self.assertIn("| rep-01 | 41/45 | 14 | 15 | 12 |", md)
        self.assertIn("**MEAN 41.50 / 45** (n=2)", md)
        self.assertIn("| Evader | 1 | 1 | 1,0 | 1,0 |", md)
        self.assertIn("## Mirror (15 episodes)", md)
        self.assertIn("wins 7 · losses 7 · draws 1 · timeouts 1", md)

    def test_probe_metrics_render_as_tables(self):
        agg = BuildAggregate().two_rep_aggregate()
        agg["probes"]["roster"] = {"facing": {"Kiter": {"meanFacingErrorDeg": 45.0}}}

        md = bench_replicates.render_markdown(agg)

        self.assertIn("### `facing` probe", md)
        self.assertIn("| meanFacingErrorDeg | 45 |", md)


class RunBenchOrchestration(unittest.TestCase):
    """The protocol drives the lane's own API — sequential replicates, then the mirror."""

    def setUp(self):
        self.calls = []

    def fake_lane(self, **kwargs):
        self.calls.append(kwargs)
        return kwargs["out_dir"] / "x-summary.json"

    def launch(self, replicates=2, mirror=True):
        with mock.patch.object(bench_replicates.eval_lane, "run_eval_lane", self.fake_lane):
            return bench_replicates.run_bench(
                onnx=Path("c.onnx"), replicates=replicates, seeds="2001,2002",
                episodes_per_seed=3, density=2.0, probes="facing,controller", mirror=mirror,
                run_root=Path("run"), project=Path("proj"), unity=Path("unity.exe"),
                lease="rl-bench", lease_wait=1800)

    def test_runs_r_roster_sessions_then_the_mirror_in_order(self):
        reps, mirror = self.launch()

        self.assertEqual([Path("run/rep-01"), Path("run/rep-02"), Path("run/mirror")],
                         [c["out_dir"] for c in self.calls])
        self.assertNotIn("opponent", self.calls[0])
        self.assertEqual("mirror", self.calls[2]["opponent"])
        self.assertEqual([Path("run/rep-01/x-summary.json"), Path("run/rep-02/x-summary.json")], reps)
        self.assertEqual(Path("run/mirror/x-summary.json"), mirror)

    def test_probes_pass_through_as_the_opaque_flag_string(self):
        self.launch()

        self.assertEqual({"facing,controller"}, {c["probes"] for c in self.calls})

    def test_no_mirror_skips_the_mirror_session(self):
        reps, mirror = self.launch(replicates=1, mirror=False)

        self.assertEqual([Path("run/rep-01")], [c["out_dir"] for c in self.calls])
        self.assertIsNone(mirror)


if __name__ == "__main__":
    unittest.main()
