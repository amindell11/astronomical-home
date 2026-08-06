import tempfile
import unittest
from collections import defaultdict, deque
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch

from mlagents.trainers.environment_parameter_manager import EnvironmentParameterManager
from mlagents.trainers.training_status import GlobalTrainingStatus

from trainer_runtime.publish import AtomicTorchModelSaver
from trainer_runtime.run_loop import OwnedRunLoop


class CompletionCriteria:
    behavior = "ShipCombat"
    min_lesson_length = 1

    def __init__(self, require_reset):
        self.require_reset = require_reset

    def need_increment(self, _progress, _rewards, _smoothed):
        return True, 1.0


def parameter_manager(require_reset=False):
    lessons = [
        SimpleNamespace(
            name="first",
            value=SimpleNamespace(seed=-1),
            completion_criteria=CompletionCriteria(require_reset),
        ),
        SimpleNamespace(
            name="second",
            value=SimpleNamespace(seed=-1),
            completion_criteria=None,
        ),
    ]
    return EnvironmentParameterManager(
        {"difficulty": SimpleNamespace(curriculum=lessons)}, run_seed=10
    )


def trainer(rewards):
    return SimpleNamespace(
        reward_buffer=deque(rewards),
        get_step=50,
        get_max_steps=100,
        end_episode=Mock(),
    )


class CurriculumLoopTests(unittest.TestCase):
    def setUp(self):
        GlobalTrainingStatus.saved_state = defaultdict(lambda: {})

    def loop(self, require_reset=False, ghost_reset=False):
        loop = OwnedRunLoop.__new__(OwnedRunLoop)
        loop.param_manager = parameter_manager(require_reset)
        loop.trainers = {
            "ShipCombat": trainer([1.0, 2.0]),
            "Other": trainer([3.0]),
        }
        loop.ghost_controller = SimpleNamespace(should_reset=lambda: ghost_reset)
        loop._reset_env = Mock()
        return loop

    def test_any_lesson_advance_clears_every_reward_buffer_without_reset(self):
        loop = self.loop()
        scheduler = SimpleNamespace(set_env_parameters=Mock())

        loop.reset_env_if_ready(scheduler)

        self.assertEqual([], list(loop.trainers["ShipCombat"].reward_buffer))
        self.assertEqual([], list(loop.trainers["Other"].reward_buffer))
        loop._reset_env.assert_not_called()
        scheduler.set_env_parameters.assert_called_once()

    def test_reset_curriculum_runs_full_reset_and_ends_trainer_episodes(self):
        loop = self.loop(require_reset=True)
        scheduler = SimpleNamespace(set_env_parameters=Mock())

        loop.reset_env_if_ready(scheduler)

        loop._reset_env.assert_called_once_with(scheduler)
        loop.trainers["ShipCombat"].end_episode.assert_called_once()
        loop.trainers["Other"].end_episode.assert_called_once()
        scheduler.set_env_parameters.assert_not_called()


class SaverInjectionTests(unittest.TestCase):
    def test_saver_is_swapped_onto_ghost_and_inner_before_create_policy(self):
        with tempfile.TemporaryDirectory() as temp:
            settings = SimpleNamespace(init_path=None, keep_checkpoints=2)
            inner = SimpleNamespace(
                trainer_settings=settings,
                artifact_path=str(Path(temp) / "ShipCombat"),
                load=False,
            )
            policy = object()
            outer = SimpleNamespace(
                trainer=inner,
                threaded=False,
                stats_reporter=object(),
                parameters=SimpleNamespace(time_horizon=64),
                add_policy=Mock(),
                publish_policy_queue=Mock(),
                subscribe_trajectory_queue=Mock(),
            )

            def create_policy(_parsed, _spec):
                self.assertIsInstance(inner.model_saver, AtomicTorchModelSaver)
                self.assertIs(outer.model_saver, inner.model_saver)
                return policy

            outer.create_policy = create_policy
            factory = SimpleNamespace(
                generate=Mock(return_value=outer),
                trainer_config={"ShipCombat": object()},
            )
            scheduler = SimpleNamespace(
                training_behaviors={"ShipCombat": object()},
                on_training_started=Mock(),
                set_agent_manager=Mock(),
                set_policy=Mock(),
            )
            loop = OwnedRunLoop.__new__(OwnedRunLoop)
            loop.trainers = {}
            loop.trainer_factory = factory
            loop.savers = {}
            loop.brain_name_to_identifier = defaultdict(set)

            manager = SimpleNamespace(policy_queue=object(), trajectory_queue=object())
            with patch("trainer_runtime.run_loop.AgentManager", return_value=manager):
                loop._create_trainer_and_manager(scheduler, "ShipCombat")

            factory.generate.assert_called_once_with("ShipCombat")
            outer.add_policy.assert_called_once()
            scheduler.on_training_started.assert_called_once()


if __name__ == "__main__":
    unittest.main()
