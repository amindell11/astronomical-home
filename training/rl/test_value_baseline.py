import json
import tempfile
import unittest
from pathlib import Path

import numpy as np

from value_baseline import (
    ArtifactMetadata,
    TrainingConfig,
    ValueBaselineError,
    audit_episodes,
    build_value_artifact,
    calibration,
    discounted_returns,
    fit_normalization,
    load_episodes,
    metric_block,
    prepare_data,
)


class ValueBaselineTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def test_terminal_returns_include_outcome_and_keep_shaping_separate(self):
        transitions = self.write_dataset(
            terminal_seeds=(11, 22, 33, 44), truncated_seeds=(11,)
        )
        episodes = load_episodes([transitions])
        config = TrainingConfig(split_counts=(2, 1, 1), min_terminal_episodes_per_seed=1)
        data = prepare_data(episodes, config)

        key = next(key for key in data.keys if key.run_seed == 11)
        index = [i for i, candidate in enumerate(data.keys) if candidate == key]
        expected_task = discounted_returns([0.1001, 0.2001, 1.3001])
        expected_envelope = discounted_returns([0.01, 0.02, 0.03])
        expected_border = discounted_returns([-0.01, -0.02, -0.03])

        np.testing.assert_allclose(data.task_returns[index], expected_task)
        np.testing.assert_allclose(data.shaping_envelope_returns[index], expected_envelope)
        np.testing.assert_allclose(data.shaping_border_returns[index], expected_border)
        censored = [row for row in data.episode_audit if row["endKind"] == "truncated"]
        self.assertEqual("censored_truncation", censored[0]["labelStatus"])
        self.assertNotIn(censored[0]["episodeId"], set(data.episode_ids.tolist()))

    def test_broken_adjacent_state_fails_with_episode_and_decision(self):
        path = self.write_dataset(terminal_seeds=(7,))
        rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
        rows[1]["state"]["combat"][0] += 1.0
        self.write_rows(path, rows)

        with self.assertRaisesRegex(ValueBaselineError, r"s7/e0/t0.*decision 2 state"):
            load_episodes([path])

    def test_collection_end_censors_only_the_last_episode_in_a_stream(self):
        path = self.write_dataset(terminal_seeds=(7,))
        rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
        tail = self.episode_rows(7, 1, terminal=True)
        tail[-1]["terminal"] = False
        tail[-1]["reward"]["outcome"] = 0.0
        tail[-1]["reward"]["total"] = sum(tail[-1]["reward"][name] for name in (
            "dense", "shapingEnvelope", "shapingBorder", "timeCost", "outcome"
        ))
        self.write_rows(path, rows + tail)

        episodes = load_episodes([path])
        self.assertEqual("collection_end", episodes[-1].end_kind)
        audit = audit_episodes(episodes, {7: "train"})
        self.assertEqual("censored_collection_end", audit[-1]["labelStatus"])

        later = self.episode_rows(7, 2, terminal=True)
        self.write_rows(path, rows + tail + later)
        with self.assertRaisesRegex(ValueBaselineError, r"only the final episode in a stream"):
            load_episodes([path])

    def test_split_keeps_every_seed_in_one_partition(self):
        path = self.write_dataset(terminal_seeds=(10, 20, 30, 40))
        episodes = load_episodes([path])
        config = TrainingConfig(split_counts=(2, 1, 1), min_terminal_episodes_per_seed=1)
        first = prepare_data(episodes, config)
        second = prepare_data(episodes, config)

        self.assertEqual(first.split_by_seed, second.split_by_seed)
        self.assertEqual({"train", "validation", "heldout"}, set(first.split_by_seed.values()))
        for seed in first.split_by_seed:
            assigned = {
                first.split_by_seed[int(candidate)]
                for candidate in first.run_seeds[first.run_seeds == seed]
            }
            self.assertEqual({first.split_by_seed[seed]}, assigned)

    def test_duplicate_and_non_finite_rows_fail_loudly(self):
        duplicate = self.write_dataset(terminal_seeds=(7,))
        rows = [json.loads(line) for line in duplicate.read_text(encoding="utf-8").splitlines()]
        self.write_rows(duplicate, rows + [rows[0]])
        with self.assertRaisesRegex(ValueBaselineError, r"duplicate .* decision 1"):
            load_episodes([duplicate])

        non_finite = self.root / "non-finite-transitions.jsonl"
        rows[0]["state"]["combat"][4] = float("nan")
        self.write_rows(non_finite, rows[:3])
        with self.assertRaisesRegex(ValueBaselineError, r"state.combat\[4\] must be finite"):
            load_episodes([non_finite])

    def test_normalization_marks_constant_features(self):
        features = np.asarray([[1.0] * 28, [3.0] + [1.0] * 27], dtype=np.float32)
        targets = np.asarray([2.0, 4.0], dtype=np.float32)

        normalization = fit_normalization(features, targets)

        self.assertEqual(2.0, normalization["inputMean"][0])
        self.assertEqual(1.0, normalization["inputStd"][0])
        self.assertEqual(27, len(normalization["constantFeatures"]))
        self.assertEqual(3.0, normalization["targetMean"])
        self.assertEqual(1.0, normalization["targetStd"])

    def test_adequacy_failure_leaves_episode_audit(self):
        transitions = self.write_dataset(terminal_seeds=(1, 2, 3, 4))
        output = self.root / "inadequate"
        config = TrainingConfig(split_counts=(2, 1, 1), min_terminal_episodes_per_seed=2)

        with self.assertRaisesRegex(ValueBaselineError, r"seed 1 .*: 1"):
            build_value_artifact(
                [transitions], output, ArtifactMetadata("inadequate", "synthetic"), config
            )

        audit = [json.loads(line) for line in (output / "episode_audit.jsonl").read_text().splitlines()]
        self.assertEqual(4, len(audit))
        self.assertEqual({"pending_terminal_return"}, {row["labelStatus"] for row in audit})

    def test_metrics_report_transition_episode_and_seed_weighting(self):
        target = np.asarray([0.0, 2.0, 10.0], dtype=np.float32)
        predicted = np.asarray([0.0, 0.0, 0.0], dtype=np.float32)
        episodes = np.asarray(["long", "long", "short"])
        seeds = np.asarray([1, 1, 2])

        result = metric_block(target, predicted, episodes, seeds)

        self.assertAlmostEqual(4.0, result["transitionWeighted"]["mae"])
        self.assertAlmostEqual(5.5, result["episodeMacro"]["mae"])
        self.assertAlmostEqual(5.5, result["seedMacro"]["mae"])

    def test_calibration_uses_equal_count_bins_and_marks_constant(self):
        target = np.arange(20, dtype=np.float32)
        predicted = target * 0.5 + 2.0
        ids = np.asarray([f"row-{index:02d}" for index in range(20)])

        result = calibration(target, predicted, ids)
        constant = calibration(target, np.ones(20, dtype=np.float32), ids)

        self.assertEqual([2] * 10, [item["count"] for item in result["bins"]])
        self.assertAlmostEqual(-4.0, result["intercept"], places=5)
        self.assertAlmostEqual(2.0, result["slope"], places=5)
        self.assertEqual("undefined_constant_prediction", constant["status"])
        self.assertEqual(1, len(constant["bins"]))

    def test_end_to_end_writes_inspectable_checked_artifact(self):
        transitions = self.write_dataset(terminal_seeds=(101, 202, 303, 404), episodes_per_seed=2)
        output = self.root / "artifact"
        config = TrainingConfig(
            split_counts=(2, 1, 1),
            min_terminal_episodes_per_seed=1,
            hidden_units=4,
            batch_size=4,
            max_epochs=5,
            patience=2,
        )
        metadata = ArtifactMetadata("test-value", "synthetic test collection")

        result = build_value_artifact([transitions], output, metadata, config)

        expected = {
            "value.onnx",
            "manifest.json",
            "metrics.json",
            "baselines.json",
            "training_history.jsonl",
            "episode_audit.jsonl",
            "heldout_predictions.jsonl",
            "verification.json",
        }
        self.assertEqual(expected, {path.name for path in output.iterdir()})
        manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8"))
        verification = json.loads((output / "verification.json").read_text(encoding="utf-8"))
        self.assertEqual("rl-value-combat-v1", manifest["stateSchema"])
        self.assertEqual(["batch", 28], manifest["input"]["shape"])
        self.assertEqual("passed", verification["checker"])
        self.assertEqual([1, 128], [row["batchSize"] for row in verification["referenceInference"]])
        self.assertIn("comparison", result["metrics"])

    def write_dataset(self, terminal_seeds, truncated_seeds=(), episodes_per_seed=1):
        path = self.root / "synthetic-transitions.jsonl"
        rows = []
        for seed in terminal_seeds:
            for episode in range(episodes_per_seed):
                rows.extend(self.episode_rows(seed, episode, terminal=True))
        for seed in truncated_seeds:
            rows.extend(self.episode_rows(seed, 100, terminal=False))
        self.write_rows(path, rows)
        return path

    @staticmethod
    def write_rows(path, rows):
        path.write_text(
            "".join(json.dumps(row, separators=(",", ":")) + "\n" for row in rows),
            encoding="utf-8",
        )

    @staticmethod
    def episode_rows(seed, episode, terminal):
        rows = []
        states = [ValueBaselineTests.combat(seed, episode, decision) for decision in range(4)]
        for decision in range(1, 4):
            dense = decision * 0.1 + seed * 1e-4 + episode * 1e-3
            outcome = 1.0 if terminal and decision == 3 else 0.0
            reward = {
                "dense": dense,
                "shapingEnvelope": decision * 0.01,
                "shapingBorder": decision * -0.01,
                "timeCost": -0.001,
                "outcome": outcome,
            }
            reward["total"] = sum(reward.values())
            rows.append({
                "schema": "rl-transition-v1",
                "observationSize": 28,
                "obstacleTokenCap": 64,
                "obstacleTokenFloats": 7,
                "continuousActionSize": 5,
                "discreteActionBranches": [2, 2],
                "rewardFields": ["dense", "shapingEnvelope", "shapingBorder", "timeCost", "outcome"],
                "runId": "synthetic",
                "workerIndex": 0,
                "arenaIndex": 0,
                "runSeed": seed,
                "episodeIndex": episode,
                "teamId": 0,
                "decision": decision,
                "state": {"combat": states[decision - 1], "obstacleTokens": []},
                "action": {"continuous": [0.0] * 5, "discrete": [0, 1], "boostExecuted": False},
                "reward": reward,
                "nextState": {"combat": states[decision], "obstacleTokens": []},
                "terminal": terminal and decision == 3,
                "truncated": not terminal and decision == 3,
            })
        return rows

    @staticmethod
    def combat(seed, episode, decision):
        return [
            ((seed % 17) * 0.01) + episode * 0.02 + decision * 0.03 + feature * 0.001
            for feature in range(28)
        ]


if __name__ == "__main__":
    unittest.main()
