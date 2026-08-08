"""Unit tests for parallel-launch transition artifact ownership."""
import tempfile
import unittest
from pathlib import Path

from run_parallel import prepare_transition_dir, transition_logs


class TransitionArtifactTests(unittest.TestCase):
    def test_force_replaces_the_previous_run_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp) / "run-id"
            stale = directory / "stamp-run-w0-transitions.jsonl"
            note = directory / "notes.txt"
            directory.mkdir()
            stale.write_text("old row\n", encoding="utf-8")
            note.write_text("keep me\n", encoding="utf-8")

            prepare_transition_dir(directory, force=True)

            self.assertFalse(stale.exists())
            self.assertTrue(note.exists())

    def test_transition_logs_are_suffix_specific_and_support_freshness_checks(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            stale = directory / "stamp-run-w0-transitions.jsonl"
            arena = directory / "stamp-run-w0-a0-transitions.jsonl"
            stale.write_text("old row\n", encoding="utf-8")
            arena.write_text("arena row\n", encoding="utf-8")
            before = transition_logs(directory, "-w0")

            fresh = directory / "later-run-w0-transitions.jsonl"
            fresh.write_text("new row\n", encoding="utf-8")

            self.assertEqual({stale, fresh}, transition_logs(directory, "-w0"))
            self.assertEqual({arena}, transition_logs(directory, "-w0-a0"))
            self.assertEqual({fresh}, transition_logs(directory, "-w0") - before)


if __name__ == "__main__":
    unittest.main()
