"""Unit tests for the lane launcher's env composition (stdlib unittest; no pytest in this venv).

    cd training/rl
    .venv\\Scripts\\python -m unittest test_eval_lane -v
"""
import contextlib
import io
import json
import sys
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
                       "RL_HARNESS_OPPONENT", "RL_HARNESS_PROBES", "RL_HARNESS_SENTENCE",
                       "RL_HARNESS_DUEL", "RL_HARNESS_MATRIX",
                       "RL_HARNESS_MATRIX_REFERENCE", "RL_HARNESS_MATRIX_ROSTER",
                       "RL_HARNESS_MATRIX_KITS", "RL_HARNESS_MATRIX_ENEMIES",
                       "RL_HARNESS_MATRIX_REFERENCE_BREAKOFF"):
            self.assertNotIn(absent, env)

    def test_sentence_param_becomes_harness_env(self):
        self.launch(seeds="2001", sentence="orbit,drift-hold")

        self.assertEqual("orbit,drift-hold", self.captured["env"]["RL_HARNESS_SENTENCE"])

    def test_duel_param_becomes_harness_env(self):
        self.launch(seeds="3001", duel="Lasers,Missiles")

        self.assertEqual("Lasers,Missiles", self.captured["env"]["RL_HARNESS_DUEL"])

    def test_matrix_param_becomes_harness_env(self):
        self.launch(seeds="3001", matrix="Lasers,Missiles")

        self.assertEqual("Lasers,Missiles", self.captured["env"]["RL_HARNESS_MATRIX"])

    def test_matrix_roster_params_become_harness_env(self):
        self.launch(seeds="3001", matrix_reference="Crimson:KiterPilot:Lasers",
                    matrix_roster="Vanguard:AggressorPilot:Lasers,Nightshade:OrbiterPilot:TurboLasers")

        env = self.captured["env"]
        self.assertEqual("Crimson:KiterPilot:Lasers", env["RL_HARNESS_MATRIX_REFERENCE"])
        self.assertEqual("Vanguard:AggressorPilot:Lasers,Nightshade:OrbiterPilot:TurboLasers",
                         env["RL_HARNESS_MATRIX_ROSTER"])
        self.assertNotIn("RL_HARNESS_MATRIX", env)

    def test_numeric_shield_token_passes_through_unchanged(self):
        self.launch(seeds="3001", matrix_reference="Crimson:AggressorPilot:Lasers:32/2/10",
                    matrix_roster="Vanguard:AggressorPilot:Lasers:28/3.5/12")

        env = self.captured["env"]
        self.assertEqual("Crimson:AggressorPilot:Lasers:32/2/10", env["RL_HARNESS_MATRIX_REFERENCE"])
        self.assertEqual("Vanguard:AggressorPilot:Lasers:28/3.5/12", env["RL_HARNESS_MATRIX_ROSTER"])

    def test_matrix_kits_param_becomes_harness_env(self):
        self.launch(seeds="3001", matrix_reference="Crimson:KiterPilot:Lasers",
                    matrix_roster="Vanguard:AggressorPilot:Lasers", matrix_kits="off")

        self.assertEqual("off", self.captured["env"]["RL_HARNESS_MATRIX_KITS"])

    def test_matrix_enemies_and_breakoff_pass_through(self):
        self.launch(seeds="3001", matrix_reference="Crimson:KiterPilot:Lasers",
                    matrix_roster="Vanguard:AggressorPilot:Lasers",
                    matrix_enemies=3, matrix_reference_breakoff="0")

        env = self.captured["env"]
        self.assertEqual("3", env["RL_HARNESS_MATRIX_ENEMIES"])
        self.assertEqual("0", env["RL_HARNESS_MATRIX_REFERENCE_BREAKOFF"])

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


class PlayerLaneComposition(unittest.TestCase):
    """Player mode leases conversion, then runs the simulation lease-free."""

    def setUp(self):
        self.out_dir = Path(tempfile.mkdtemp())
        self.captured = {}

    def fake_run_batch(self, lease, project, batch_script, env, wait_seconds, log_path):
        self.captured.update(batch_script=batch_script, env=env)
        return 0

    def test_convert_step_runs_the_convert_child_with_bundle_inputs(self):
        with mock.patch.object(eval_lane, "run_batch", self.fake_run_batch):
            bundle = eval_lane.run_convert_step(project=Path("proj"), unity=Path("unity.exe"),
                                                lease="test-lease", out_dir=self.out_dir,
                                                onnx="ckpt/ShipCombat-42.onnx", opponent="mirror")

        self.assertEqual(eval_lane.CONVERT_CHILD, self.captured["batch_script"])
        env = self.captured["env"]
        self.assertEqual("ckpt/ShipCombat-42.onnx", env["RL_HARNESS_ONNX"])
        self.assertEqual("mirror", env["RL_HARNESS_OPPONENT"])
        expected = self.out_dir / "eval-models.bundle"
        self.assertEqual(str(expected), env["RL_HARNESS_BUNDLE"])
        self.assertEqual(expected, bundle)

    def test_player_launch_is_lease_free_and_carries_the_bundle_var(self):
        def fake_run(cmd, env):
            self.captured.update(cmd=cmd, env=env)
            (self.out_dir / "20260731-120000-heldout-summary.json").write_text("{}")
            return mock.Mock(returncode=0)

        with mock.patch.object(eval_lane.subprocess, "run", fake_run):
            summary = eval_lane.run_player_eval(exe=Path("RLHarnessEval.exe"),
                                                bundle=self.out_dir / "eval-models.bundle",
                                                out_dir=self.out_dir, onnx="ckpt/ShipCombat-42.onnx",
                                                seeds="2001,2002")

        self.assertEqual(["RLHarnessEval.exe", "-batchmode", "-nographics",
                          "-logFile", str(self.out_dir / "player.log")], self.captured["cmd"])
        env = self.captured["env"]
        self.assertEqual(str(self.out_dir / "eval-models.bundle"), env["RL_HARNESS_BUNDLE"])
        self.assertEqual("ckpt/ShipCombat-42.onnx", env["RL_HARNESS_ONNX"])
        self.assertEqual("2001,2002", env["RL_HARNESS_SEEDS"])
        self.assertEqual(str(self.out_dir), env["RL_HARNESS_OUT_DIR"])
        self.assertNotIn("HARNESS_UNITY", env, "editor-child plumbing must not reach the player")
        self.assertEqual(self.out_dir / "20260731-120000-heldout-summary.json", summary)

    def test_player_nonzero_exit_fails_loud(self):
        with mock.patch.object(eval_lane.subprocess, "run",
                               lambda cmd, env: mock.Mock(returncode=1)):
            with self.assertRaises(SystemExit):
                eval_lane.run_player_eval(exe=Path("RLHarnessEval.exe"),
                                          bundle=self.out_dir / "eval-models.bundle",
                                          out_dir=self.out_dir, onnx="ckpt/ShipCombat-42.onnx",
                                          seeds="2001")


class MatrixKitsCli(unittest.TestCase):
    def _main(self, argv):
        stderr = io.StringIO()
        with mock.patch.object(sys, "argv", argv):
            with self.assertRaises(SystemExit) as caught:
                with contextlib.redirect_stderr(stderr):
                    eval_lane.main()
        self.assertEqual(2, caught.exception.code)
        return stderr.getvalue()

    def test_refused_without_matrix_roster(self):
        message = self._main(["eval_lane.py", "--seeds", "3001", "--matrix-kits", "off"])
        self.assertIn("--matrix-kits requires --matrix-roster", message)

    def test_refused_with_player_exec(self):
        with tempfile.NamedTemporaryFile(suffix=".onnx") as onnx:
            message = self._main([
                "eval_lane.py", "--seeds", "3001", "--exec", "player", "--onnx", onnx.name,
                "--matrix-reference", "Crimson:KiterPilot:Lasers",
                "--matrix-roster", "Vanguard:AggressorPilot:Lasers",
                "--matrix-kits", "off",
            ])
        self.assertIn("--matrix-kits is editor-only", message)

    def test_enemies_refused_without_matrix_roster(self):
        message = self._main(["eval_lane.py", "--seeds", "3001", "--matrix-enemies", "3"])
        self.assertIn("--matrix-enemies requires --matrix-roster", message)

    def test_breakoff_refused_without_matrix_roster(self):
        message = self._main(["eval_lane.py", "--seeds", "3001", "--matrix-reference-breakoff", "0"])
        self.assertIn("--matrix-reference-breakoff requires --matrix-roster", message)

    def test_enemies_and_breakoff_refused_with_player_exec(self):
        with tempfile.NamedTemporaryFile(suffix=".onnx") as onnx:
            enemies = self._main([
                "eval_lane.py", "--seeds", "3001", "--exec", "player", "--onnx", onnx.name,
                "--matrix-reference", "Crimson:KiterPilot:Lasers",
                "--matrix-roster", "Vanguard:AggressorPilot:Lasers",
                "--matrix-enemies", "3",
            ])
            breakoff = self._main([
                "eval_lane.py", "--seeds", "3001", "--exec", "player", "--onnx", onnx.name,
                "--matrix-reference", "Crimson:KiterPilot:Lasers",
                "--matrix-roster", "Vanguard:AggressorPilot:Lasers",
                "--matrix-reference-breakoff", "0",
            ])
        self.assertIn("--matrix-enemies is editor-only", enemies)
        self.assertIn("--matrix-reference-breakoff is editor-only", breakoff)


class NumericShieldStem(unittest.TestCase):
    def _run(self, reference, roster):
        project = Path(tempfile.mkdtemp())
        self.addCleanup(project.rmdir)
        captured = {}

        def fake_run_eval_lane(**kwargs):
            captured.update(kwargs)
            return project / "summary.json"

        argv = [
            "eval_lane.py", "--seeds", "3001", "--project", str(project), "--unity", "Unity.exe",
            "--matrix-reference", reference, "--matrix-roster", roster,
        ]
        with mock.patch.object(sys, "argv", argv), \
                mock.patch.object(eval_lane, "HARNESS_CHILD", Path(__file__)), \
                mock.patch.object(eval_lane, "run_eval_lane", fake_run_eval_lane):
            eval_lane.main()
        return captured

    def test_slash_in_the_reference_does_not_split_the_out_dir(self):
        captured = self._run("Crimson:AggressorPilot:Lasers:32/2/10", "Vanguard:AggressorPilot:Lasers")

        out_dir = captured["out_dir"]
        self.assertEqual(eval_lane.MANUAL_ROOT, out_dir.parent)
        self.assertTrue(out_dir.name.startswith("matrix-roster-crimson-aggressorpilot-lasers-32-2-10-"))
        self.assertEqual("Crimson:AggressorPilot:Lasers:32/2/10", captured["matrix_reference"])


if __name__ == "__main__":
    unittest.main()
