"""Unit tests for the lane launcher's env composition (stdlib unittest; no pytest in this venv).

    cd training/rl
    .venv\\Scripts\\python -m unittest test_eval_lane -v
"""
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import eval_lane


class LauncherEnvComposition(unittest.TestCase):
    """The child env comes from explicit params only: inherited RL_HARNESS_* overrides and
    retired RL_EVAL_* names are stripped, so a stale shell can never reshape a run."""

    def setUp(self):
        self.out_dir = Path(tempfile.mkdtemp())
        self.captured = {}

    def fake_run_batch(self, lease, project, batch_script, env, wait_seconds, log_path):
        self.captured.update(lease=lease, project=project, batch_script=batch_script,
                             env=env, wait_seconds=wait_seconds, log_path=log_path)
        (self.out_dir / "20260731-120000-custom-summary.json").write_text(json.dumps({}))
        return 0

    def launch(self, **kwargs):
        with mock.patch.object(eval_lane, "run_batch", self.fake_run_batch):
            return eval_lane.run_eval_lane(project=Path("proj"), unity=Path("unity.exe"),
                                           lease="test-lease", out_dir=self.out_dir, **kwargs)

    def test_passed_params_become_harness_env_as_strings(self):
        self.launch(onnx="ckpt/ShipCombat-42.onnx", seeds="2001,2002", episodes_per_seed=3)

        env = self.captured["env"]
        self.assertEqual("ckpt/ShipCombat-42.onnx", env["RL_HARNESS_ONNX"])
        self.assertEqual("2001,2002", env["RL_HARNESS_SEEDS"])
        self.assertEqual("3", env["RL_HARNESS_EPISODES_PER_SEED"])
        self.assertEqual(str(self.out_dir), env["RL_HARNESS_OUT_DIR"])
        self.assertEqual("unity.exe", env["HARNESS_UNITY"])
        self.assertEqual("proj", env["HARNESS_PROJ"])
        self.assertEqual(str(self.out_dir / "editor.log"), env["HARNESS_LOG"])

    def test_omitted_params_stay_unset_so_sessionspec_defaults_apply(self):
        self.launch(seeds="2001,2002")

        env = self.captured["env"]
        for absent in ("RL_HARNESS_ONNX", "RL_HARNESS_EPISODES_PER_SEED", "RL_HARNESS_DENSITY",
                       "RL_HARNESS_OPPONENT", "RL_HARNESS_PROBES"):
            self.assertNotIn(absent, env)

    def test_inherited_harness_and_retired_eval_names_are_stripped(self):
        inherited = {
            "RL_HARNESS_DENSITY": "3.0",
            "RL_HARNESS_OPPONENT": "mirror",
            "RL_EVAL_ONNX": "stale.onnx",
            "RL_EVAL_SEEDS": "1,2,3",
            "RL_EVAL_OUT_DIR": "stale-dir",
            "UNRELATED_VAR": "survives",
        }
        with mock.patch.dict("os.environ", inherited):
            self.launch(seeds="2001,2002")

        env = self.captured["env"]
        self.assertEqual("survives", env["UNRELATED_VAR"])
        leaked = [k for k in env if k.startswith("RL_EVAL_")]
        self.assertEqual([], leaked, "retired names must never reach the child")
        self.assertNotIn("RL_HARNESS_DENSITY", env)
        self.assertNotIn("RL_HARNESS_OPPONENT", env)
        self.assertEqual("2001,2002", env["RL_HARNESS_SEEDS"], "explicit params win over the strip")

    def test_returns_the_one_summary_from_the_dir_it_named(self):
        summary = self.launch(seeds="2001,2002")

        self.assertEqual(self.out_dir / "20260731-120000-custom-summary.json", summary)

    def test_nonzero_child_exit_fails_loud(self):
        def failing_run_batch(lease, project, batch_script, env, wait_seconds, log_path):
            return 1

        with mock.patch.object(eval_lane, "run_batch", failing_run_batch):
            with self.assertRaises(SystemExit):
                eval_lane.run_eval_lane(project=Path("proj"), unity=Path("unity.exe"),
                                        lease="test-lease", out_dir=self.out_dir, seeds="2001")


if __name__ == "__main__":
    unittest.main()
