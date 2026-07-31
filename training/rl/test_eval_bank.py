"""Unit tests for the banking CLI's paired A/B reading (stdlib unittest; no pytest in this venv).

    cd training/rl
    .venv\\Scripts\\python -m unittest test_eval_bank -v
"""
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import eval_bank
from eval_bank import (draw_held_out, episode_outcomes, paired_discordance, record_draw,
                       run_side, side_stats)
from eval_bundle import load_bundle
from eval_gate import SUMMARY_SCHEMA

BUNDLE = load_bundle()
SEEDS = (2001, 2002)
ARCHETYPES = ("Aggressor", "Evader")


def write_replicate(directory: Path, outcomes: dict, name="20260731-000000-custom") -> Path:
    """One synthetic replicate: episode JSONL rows plus the v2 summary that names it.
    outcomes: (seed, opponent, episodeIndex) -> won."""
    directory.mkdir(parents=True, exist_ok=True)
    jsonl = directory / f"{name}.jsonl"
    lines = [json.dumps({"spec": {"runSeed": seed}, "opponent": {"archetype": opponent},
                         "episodeIndex": index, "outcome": "Win" if won else "Loss"})
             for (seed, opponent, index), won in outcomes.items()]
    jsonl.write_text("\n".join(lines) + "\n")
    tally = {}
    for (seed, opponent, index), won in outcomes.items():
        wins, episodes = tally.get(opponent, (0, 0))
        tally[opponent] = (wins + (1 if won else 0), episodes + 1)
    summary = directory / f"{name}-summary.json"
    # episodesJsonl deliberately records a foreign absolute path: resolution must go by basename.
    summary.write_text(json.dumps({
        "schema": SUMMARY_SCHEMA,
        "opponents": [{"opponent": o, "wins": w, "episodes": n} for o, (w, n) in tally.items()],
        "episodesJsonl": str(Path("D:/somewhere/else") / jsonl.name),
    }))
    return summary


def grid(won_by_key=None, default=True):
    outcomes = {}
    for seed in SEEDS:
        for opponent in ARCHETYPES:
            for index in range(2):
                key = (seed, opponent, index)
                outcomes[key] = (won_by_key or {}).get(key, default)
    return outcomes


class EpisodeOutcomes(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())

    def test_reads_outcomes_keyed_by_episode_identity(self):
        summary = write_replicate(self.dir, grid({(2001, "Evader", 1): False}))
        outcomes = episode_outcomes(summary)
        self.assertEqual(8, len(outcomes))
        self.assertFalse(outcomes[(2001, "Evader", 1)])
        self.assertTrue(outcomes[(2002, "Aggressor", 0)])

    def test_duplicate_episode_identity_fails_loud(self):
        summary = write_replicate(self.dir, grid())
        jsonl = self.dir / "20260731-000000-custom.jsonl"
        jsonl.write_text(jsonl.read_text() + jsonl.read_text().splitlines()[0] + "\n")
        with self.assertRaises(SystemExit):
            episode_outcomes(summary)

    def test_missing_jsonl_fails_loud(self):
        summary = write_replicate(self.dir, grid())
        (self.dir / "20260731-000000-custom.jsonl").unlink()
        with self.assertRaises(SystemExit):
            episode_outcomes(summary)


class PairedDiscordance(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp())

    def replicate(self, side, rep, outcomes):
        return write_replicate(self.root / side / f"rep-{rep}", outcomes,
                               name=f"20260731-00000{rep}-custom")

    def test_counts_only_discordant_pairs(self):
        candidate = [self.replicate("candidate", 1, grid({(2001, "Evader", 0): False,
                                                          (2001, "Evader", 1): False}))]
        incumbent = [self.replicate("incumbent", 1, grid({(2001, "Evader", 0): False,
                                                          (2002, "Aggressor", 1): False}))]
        pairs, candidate_only, incumbent_only = paired_discordance(candidate, incumbent)
        self.assertEqual(8, pairs)
        self.assertEqual(1, candidate_only)  # (2002, Aggressor, 1): candidate won, incumbent lost
        self.assertEqual(1, incumbent_only)  # (2001, Evader, 1): incumbent won, candidate lost

    def test_pairs_accumulate_across_replicates(self):
        candidate = [self.replicate("candidate", k, grid()) for k in (1, 2)]
        incumbent = [self.replicate("incumbent", k, grid(default=False)) for k in (1, 2)]
        pairs, candidate_only, incumbent_only = paired_discordance(candidate, incumbent)
        self.assertEqual(16, pairs)
        self.assertEqual(16, candidate_only)
        self.assertEqual(0, incumbent_only)

    def test_mismatched_episode_identities_fail_loud(self):
        candidate = [self.replicate("candidate", 1, grid())]
        drifted = grid()
        drifted[(2001, "Orbiter", 0)] = drifted.pop((2001, "Evader", 0))
        incumbent = [self.replicate("incumbent", 1, drifted)]
        with self.assertRaises(SystemExit):
            paired_discordance(candidate, incumbent)

    def test_unequal_replicate_counts_fail_loud(self):
        candidate = [self.replicate("candidate", k, grid()) for k in (1, 2)]
        incumbent = [self.replicate("incumbent", 1, grid())]
        with self.assertRaises(SystemExit):
            paired_discordance(candidate, incumbent)


class SideStats(unittest.TestCase):
    def test_pools_per_opponent_cells_and_replicate_totals(self):
        root = Path(tempfile.mkdtemp())
        first = write_replicate(root / "rep-1", grid({(2001, "Evader", 0): False}))
        second = write_replicate(root / "rep-2", grid(), name="20260731-000001-custom")
        stats = side_stats([first, second])
        self.assertEqual([7, 8], stats["replicateTotals"])
        by_name = {o["opponent"]: o for o in stats["opponents"]}
        self.assertEqual(7, by_name["Evader"]["wins"])
        self.assertEqual(8, by_name["Evader"]["episodes"])
        self.assertEqual(15, stats["totalWins"])
        self.assertEqual(16, stats["totalEpisodes"])
        self.assertEqual(2, len(by_name["Evader"]["wilson95"]))


class HeldOutRegistry(unittest.TestCase):
    def setUp(self):
        root = Path(tempfile.mkdtemp())
        self.registry = root / "heldout_draws.jsonl"
        self.out_dir = root / "held-out" / "rep-1"

    def registry_lines(self):
        if not self.registry.exists():
            return []
        return [json.loads(line) for line in self.registry.read_text().splitlines()]

    def test_every_draw_appends_one_auditable_line(self):
        record_draw(self.registry, {"candidate": "a.onnx"})
        record_draw(self.registry, {"candidate": "b.onnx"})
        self.assertEqual(["a.onnx", "b.onnx"], [line["candidate"] for line in self.registry_lines()])

    def test_exposure_is_logged_before_the_draw_runs(self):
        def crashing_launch(out_dir, onnx, seeds):
            raise RuntimeError("editor died mid-draw")

        with mock.patch.object(eval_bank, "REGISTRY", self.registry):
            with self.assertRaises(RuntimeError):
                draw_held_out(Path("cand.onnx"), self.out_dir, BUNDLE, crashing_launch)
        lines = self.registry_lines()
        self.assertEqual(1, len(lines), "a crashed draw must still be a recorded exposure")
        self.assertEqual("cand.onnx", lines[0]["candidate"])
        self.assertEqual(str(self.out_dir), lines[0]["outDir"])

    def test_replayed_draw_logs_no_new_exposure(self):
        write_replicate(self.out_dir, grid())

        def forbidden_launch(out_dir, onnx, seeds):
            self.fail("a summarized draw must replay, not re-run")

        with mock.patch.object(eval_bank, "REGISTRY", self.registry):
            block = draw_held_out(Path("cand.onnx"), self.out_dir, BUNDLE, forbidden_launch)
        self.assertEqual([], self.registry_lines(), "replay is not a second exposure")
        self.assertEqual(2, len(block["opponents"]))


class RunSideResume(unittest.TestCase):
    def test_resume_runs_only_the_missing_replicates(self):
        side_dir = Path(tempfile.mkdtemp()) / "candidate"
        for k in (1, 2, 4):
            d = side_dir / f"rep-{k}"
            d.mkdir(parents=True)
            (d / f"20260731-00000{k}-custom-summary.json").write_text("{}")
        launched = []

        def launch(out_dir, onnx, seeds):
            launched.append(out_dir.name)
            out_dir.mkdir(parents=True, exist_ok=True)
            path = out_dir / "20260731-000009-custom-summary.json"
            path.write_text("{}")
            return path

        summaries = run_side("candidate", Path("cand.onnx"), side_dir, BUNDLE, launch)
        self.assertEqual(["rep-3", "rep-5"], launched)
        self.assertEqual(BUNDLE.k_bank, len(summaries))


if __name__ == "__main__":
    unittest.main()
