import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from mlagents_envs.exception import UnityCommunicationException

from trainer_runtime.env_scheduler import (
    EnvironmentCommand,
    EnvironmentResponse,
    EnvironmentStep,
    StepResponse,
    SubprocessEnvScheduler,
)
from trainer_runtime.microbatch import MicrobatchSettings
from trainer_runtime.run_loop import create_environment_factory


class FakeProcess:
    def is_alive(self):
        return False

    def terminate(self):
        raise AssertionError("healthy fake worker must not be terminated")


class FakeWorker:
    def __init__(self, worker_id, step_queue, respond=False):
        self.worker_id = worker_id
        self.step_queue = step_queue
        self.respond = respond
        self.previous_step = EnvironmentStep.empty(worker_id)
        self.previous_all_action_info = {}
        self.waiting = False
        self.closed = False
        self.process = FakeProcess()
        self.sent = []

    def send(self, command, payload=None):
        self.sent.append((command, payload))
        if command == EnvironmentCommand.STEP and self.respond:
            self.step_queue.put(EnvironmentResponse(
                EnvironmentCommand.STEP,
                self.worker_id,
                StepResponse({}, None, {}),
            ))

    def request_close(self):
        pass


def run_options():
    env_settings = SimpleNamespace(
        max_lifetime_restarts=3,
        restarts_rate_limit_n=3,
        restarts_rate_limit_period_s=60,
    )
    return SimpleNamespace(env_settings=env_settings)


class SchedulerTests(unittest.TestCase):
    def test_batched_ready_poll_waits_only_the_fixed_collection_window(self):
        def factory(worker_id, queue, _env_factory, _options):
            return FakeWorker(worker_id, queue, respond=worker_id == 0)

        scheduler = SubprocessEnvScheduler(
            lambda *_args: None,
            run_options(),
            2,
            factory,
            MicrobatchSettings.create(2, 2),
        )

        with patch(
            "trainer_runtime.env_scheduler.time.perf_counter",
            side_effect=[1.0, 1.0004, 1.0005],
        ) as clock:
            steps = scheduler._step()

        self.assertEqual([0], [step.worker_id for step in steps])
        self.assertEqual(3, clock.call_count)
        scheduler.step_queue.close()
        scheduler.step_queue.join_thread()

    def test_batched_ready_poll_collects_only_the_worker_cap(self):
        def factory(worker_id, queue, _env_factory, _options):
            return FakeWorker(worker_id, queue, respond=True)

        scheduler = SubprocessEnvScheduler(
            lambda *_args: None,
            run_options(),
            3,
            factory,
            MicrobatchSettings.create(2, 3),
        )

        with patch(
            "trainer_runtime.env_scheduler.time.perf_counter",
            side_effect=[1.0, 1.0001],
        ):
            steps = scheduler._step()

        self.assertEqual([0, 1], [step.worker_id for step in steps])
        self.assertTrue(scheduler.env_workers[2].waiting)
        scheduler.step_queue.close()
        scheduler.step_queue.join_thread()

    def test_batched_ready_poll_does_not_accept_queued_work_after_deadline(self):
        def factory(worker_id, queue, _env_factory, _options):
            return FakeWorker(worker_id, queue, respond=True)

        scheduler = SubprocessEnvScheduler(
            lambda *_args: None,
            run_options(),
            2,
            factory,
            MicrobatchSettings.create(2, 2),
        )

        with patch(
            "trainer_runtime.env_scheduler.time.perf_counter",
            side_effect=[1.0, 1.0005],
        ):
            steps = scheduler._step()

        self.assertEqual([0], [step.worker_id for step in steps])
        self.assertTrue(scheduler.env_workers[1].waiting)
        scheduler.step_queue.close()
        scheduler.step_queue.join_thread()

    def test_ready_poll_queues_every_idle_worker_and_returns_first_response(self):
        def factory(worker_id, queue, _env_factory, _options):
            return FakeWorker(worker_id, queue, respond=worker_id == 1)

        scheduler = SubprocessEnvScheduler(lambda *_args: None, run_options(), 2, factory)

        steps = scheduler._step()

        self.assertEqual([1], [step.worker_id for step in steps])
        self.assertTrue(scheduler.env_workers[0].waiting)
        self.assertFalse(scheduler.env_workers[1].waiting)
        self.assertEqual(EnvironmentCommand.STEP, scheduler.env_workers[0].sent[0][0])
        self.assertEqual(EnvironmentCommand.STEP, scheduler.env_workers[1].sent[0][0])
        scheduler.step_queue.close()
        scheduler.step_queue.join_thread()

    def test_recoverable_worker_failure_restarts_then_full_resets(self):
        created = []

        def factory(worker_id, queue, _env_factory, _options):
            worker = FakeWorker(worker_id, queue)
            created.append(worker)
            return worker

        scheduler = SubprocessEnvScheduler(lambda *_args: None, run_options(), 1, factory)
        scheduler.env_parameters = {"difficulty": "sampler"}
        reset_configs = []
        scheduler.reset = reset_configs.append

        scheduler._restart_failed_workers(EnvironmentResponse(
            EnvironmentCommand.ENV_EXITED, 0, UnityCommunicationException("lost")
        ))

        self.assertEqual(2, len(created))
        self.assertEqual([{"difficulty": "sampler"}], reset_configs)
        self.assertEqual([1], scheduler.restart_counts)
        scheduler.step_queue.close()
        scheduler.step_queue.join_thread()

    def test_environment_factory_preserves_pacing_inputs_seed_and_worker_index(self):
        captured = {}

        def fake_unity_environment(**kwargs):
            captured.update(kwargs)
            return object()

        factory = create_environment_factory(
            "player.exe", True, False, 70, 6, 300, 5006,
            ["--harness-base-port", "5006"], "logs",
        )
        with patch("trainer_runtime.run_loop.UnityEnvironment", fake_unity_environment):
            factory(3, ["side-channel"])

        self.assertEqual(73, captured["seed"])
        self.assertEqual(3, captured["worker_id"])
        self.assertEqual(5006, captured["base_port"])
        self.assertEqual(
            ["--harness-base-port", "5006", "--harness-worker-index", "3"],
            captured["additional_args"],
        )
        self.assertEqual(["side-channel"], captured["side_channels"])


if __name__ == "__main__":
    unittest.main()
