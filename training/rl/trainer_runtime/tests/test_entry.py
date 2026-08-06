import hashlib
import os
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from mlagents.trainers.stats import StatsSummary
from mlagents_envs.side_channel.stats_side_channel import StatsAggregationMethod

from run_parallel import MLAGENTS, owned_runtime_args, trainer_command
from trainer_runtime.contract import manifest_path, read_manifest, read_summaries, summaries_path
from trainer_runtime.entry import (
    _mode,
    extract_microbatch_worker_cap,
    owned_stats_writers,
    refuse_conflicting_restore,
)
from trainer_runtime.microbatch import MicrobatchSettings
from trainer_runtime.run_loop import validate_owned_options, validate_resume_microbatch
from trainer_runtime.stats_writer import JsonlStatsWriter


class OwnedEntryTests(unittest.TestCase):
    def test_owned_cap_is_removed_before_mlagents_parse_and_env_args_are_untouched(self):
        args, cap = extract_microbatch_worker_cap([
            "config.yaml", "--microbatch-worker-cap", "8", "--env-args",
            "--microbatch-worker-cap", "unity-value",
        ])
        self.assertEqual(8, cap)
        self.assertEqual([
            "config.yaml", "--env-args", "--microbatch-worker-cap", "unity-value"
        ], args)

    def test_owned_cap_defaults_to_one_and_rejects_invalid_values(self):
        self.assertEqual((['config.yaml'], 1), extract_microbatch_worker_cap(['config.yaml']))
        for value in ("0", "-2", "many"):
            with self.subTest(value=value), self.assertRaisesRegex(SystemExit, "positive integer"):
                extract_microbatch_worker_cap([
                    "config.yaml", "--microbatch-worker-cap", value
                ])

    def test_restore_conflict_fails_before_env_args(self):
        with self.assertRaisesRegex(SystemExit, "mutually exclusive"):
            refuse_conflicting_restore(["config.yaml", "--resume", "--initialize-from", "seed"])

    def test_restore_words_forwarded_to_unity_are_not_runtime_flags(self):
        refuse_conflicting_restore([
            "config.yaml", "--resume", "--env-args", "--initialize-from", "unity-value"
        ])

    def test_launcher_runtime_selector_uses_owned_module_or_stock_executable(self):
        self.assertEqual([os.sys.executable, "-m", "trainer_runtime.entry"], trainer_command("owned"))
        self.assertEqual([str(MLAGENTS)], trainer_command("ml-agents"))
        self.assertEqual(
            ["--microbatch-worker-cap", "6"], owned_runtime_args("owned", 6)
        )
        self.assertEqual([], owned_runtime_args("ml-agents", 6))

    def test_hybrid_mode_is_explicit_instead_of_inferred_from_elo(self):
        with patch.dict(os.environ, {"RL_HYBRID_SCRIPTED_WORKERS": "2"}):
            self.assertEqual("hybrid", _mode(self_play=True))

    def test_threaded_trainer_is_refused_before_launch(self):
        options = SimpleNamespace(behaviors={"ShipCombat": SimpleNamespace(threaded=True)})

        with self.assertRaisesRegex(RuntimeError, "does not support threaded"):
            validate_owned_options(options)

    def test_plugin_writes_manifest_and_structured_summary(self):
        with tempfile.TemporaryDirectory() as temp:
            results_dir = Path(temp).resolve()
            run_dir = results_dir / "owned-run"
            run_dir.mkdir()
            config = results_dir / "config.yaml"
            config.write_text("behaviors: {}\n", encoding="utf-8")
            settings = SimpleNamespace(max_steps=4000, self_play=None)
            checkpoint = SimpleNamespace(
                results_dir=str(results_dir), run_id="owned-run", resume=False
            )
            options = SimpleNamespace(
                behaviors={"ShipCombat": settings}, checkpoint_settings=checkpoint
            )

            writers = owned_stats_writers(
                options,
                config,
                datetime(2026, 8, 5, 19, 0, tzinfo=timezone.utc),
                MicrobatchSettings.create(8, 6),
            )
            writers[0].write_stats("ShipCombat", {
                "Environment/Cumulative Reward": StatsSummary(
                    [-1.0, 1.0], StatsAggregationMethod.AVERAGE
                ),
                "Self-play/ELO": StatsSummary([1210.0], StatsAggregationMethod.MOST_RECENT),
                "Is Training": StatsSummary([1.0], StatsAggregationMethod.MOST_RECENT),
            }, 1000)

            manifest = read_manifest(manifest_path(results_dir, "owned-run"))
            summaries = read_summaries(summaries_path(manifest.run_dir))
            self.assertEqual("ShipCombat", manifest.behavior)
            self.assertEqual("scripted", manifest.mode)
            self.assertEqual(hashlib.sha256(config.read_bytes()).hexdigest(), manifest.config_hash)
            self.assertEqual(8, manifest.microbatch_worker_cap)
            self.assertEqual(6, manifest.microbatch_effective_worker_cap)
            self.assertEqual(500, manifest.microbatch_window_micros)
            self.assertEqual(1000, summaries[0].step)
            self.assertEqual(0.0, summaries[0].mean_reward)
            self.assertEqual(1.0, summaries[0].reward_std_dev)
            self.assertEqual(1210.0, summaries[0].elo)
            self.assertTrue(summaries[0].is_training)

    def test_resume_keeps_elapsed_monotonic_across_legs(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "summaries.jsonl"
            first_leg = JsonlStatsWriter(path, "ShipCombat", 4000, resume=False)
            first_leg.started -= 500.0
            first_leg.write_stats("ShipCombat", {}, 1000)

            resumed = JsonlStatsWriter(path, "ShipCombat", 4000, resume=True)
            resumed.write_stats("ShipCombat", {}, 2000)

            rows = read_summaries(path)
            self.assertEqual([1000, 2000], [row.step for row in rows])
            self.assertGreaterEqual(rows[1].elapsed_seconds, rows[0].elapsed_seconds)

    def test_resume_requires_the_recorded_effective_schedule(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "run_manifest.json"
            path.write_text(
                '{"runId":"run","behavior":"ShipCombat","resultsDir":"results",'
                '"startedAt":"2026-08-06T00:00:00Z","maxSteps":10,"mode":"scripted",'
                '"configHash":"hash","microbatchWorkerCap":8,'
                '"microbatchEffectiveWorkerCap":6,"microbatchWindowMicros":500}\n',
                encoding="utf-8",
            )
            validate_resume_microbatch(path, True, MicrobatchSettings.create(6, 6))
            with self.assertRaisesRegex(RuntimeError, "schedule mismatch"):
                validate_resume_microbatch(path, True, MicrobatchSettings.create(1, 6))

    def test_legacy_resume_is_sequential_only(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "run_manifest.json"
            path.write_text(
                '{"runId":"run","behavior":"ShipCombat","resultsDir":"results",'
                '"startedAt":"2026-08-06T00:00:00Z","maxSteps":10,"mode":"scripted",'
                '"configHash":"hash"}\n',
                encoding="utf-8",
            )
            validate_resume_microbatch(path, True, MicrobatchSettings.create(1, 6))
            with self.assertRaisesRegex(RuntimeError, "legacy run.*cap 1"):
                validate_resume_microbatch(path, True, MicrobatchSettings.create(6, 6))


if __name__ == "__main__":
    unittest.main()
