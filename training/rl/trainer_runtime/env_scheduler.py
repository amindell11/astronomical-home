import datetime
import enum
import time
from multiprocessing import Pipe, Process, Queue
from multiprocessing.connection import Connection
from queue import Empty as EmptyQueueException
from typing import Any, Callable, Dict, Iterable, List, NamedTuple, Optional, Set, Tuple

import cloudpickle
from mlagents.trainers.action_info import ActionInfo
from mlagents.trainers.agent_processor import AgentManager, AgentManagerQueue
from mlagents.trainers.settings import ParameterRandomizationSettings, RunOptions, TrainerSettings
from mlagents.trainers.training_analytics_side_channel import TrainingAnalyticsSideChannel
from mlagents_envs import logging_util
from mlagents_envs.base_env import BaseEnv, BehaviorName, BehaviorSpec, DecisionSteps, TerminalSteps
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.exception import (
    UnityCommunicationException,
    UnityCommunicatorStoppedException,
    UnityEnvironmentException,
    UnityTimeOutException,
)
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfig, EngineConfigurationChannel
from mlagents_envs.side_channel.environment_parameters_channel import EnvironmentParametersChannel
from mlagents_envs.side_channel.side_channel import SideChannel
from mlagents_envs.side_channel.stats_side_channel import EnvironmentStats, StatsSideChannel
from mlagents_envs.timers import (
    TimerNode,
    get_timer_root,
    hierarchical_timer,
    reset_timers,
    timed,
)

from trainer_runtime.microbatch import InferenceMicrobatch, MicrobatchSettings

logger = logging_util.get_logger(__name__)
WORKER_SHUTDOWN_TIMEOUT_S = 10

AllStepResult = Dict[BehaviorName, Tuple[DecisionSteps, TerminalSteps]]


class EnvironmentStep(NamedTuple):
    current_all_step_result: AllStepResult
    worker_id: int
    brain_name_to_action_info: Dict[BehaviorName, ActionInfo]
    environment_stats: EnvironmentStats

    @property
    def name_behavior_ids(self) -> Iterable[BehaviorName]:
        return self.current_all_step_result.keys()

    @staticmethod
    def empty(worker_id: int) -> "EnvironmentStep":
        return EnvironmentStep({}, worker_id, {}, {})


class EnvironmentScheduler:
    def __init__(self):
        self.policies = {}
        self.agent_managers: Dict[BehaviorName, AgentManager] = {}
        self.first_step_infos: List[EnvironmentStep] = []

    def set_policy(self, behavior_name: BehaviorName, policy) -> None:
        self.policies[behavior_name] = policy
        if behavior_name in self.agent_managers:
            self.agent_managers[behavior_name].policy = policy

    def set_agent_manager(self, behavior_name: BehaviorName, manager: AgentManager) -> None:
        self.agent_managers[behavior_name] = manager

    def reset(self, config: Optional[Dict] = None) -> int:
        for manager in self.agent_managers.values():
            manager.end_episode()
        self.first_step_infos = self._reset_env(config)
        return len(self.first_step_infos)

    def get_steps(self) -> List[EnvironmentStep]:
        if self.first_step_infos:
            self._process_step_infos(self.first_step_infos)
            self.first_step_infos = []
        for behavior_name in self.agent_managers:
            policy = None
            try:
                while True:
                    policy = self.agent_managers[behavior_name].policy_queue.get_nowait()
            except AgentManagerQueue.Empty:
                if policy is not None:
                    self.set_policy(behavior_name, policy)
        return self._step()

    def process_steps(self, step_infos: List[EnvironmentStep]) -> int:
        return self._process_step_infos(step_infos)

    def _process_step_infos(self, step_infos: List[EnvironmentStep]) -> int:
        for step_info in step_infos:
            for behavior_id in step_info.name_behavior_ids:
                if behavior_id not in self.agent_managers:
                    logger.warning(f"Agent manager was not created for behavior id {behavior_id}.")
                    continue
                decision_steps, terminal_steps = step_info.current_all_step_result[behavior_id]
                manager = self.agent_managers[behavior_id]
                manager.add_experiences(
                    decision_steps,
                    terminal_steps,
                    step_info.worker_id,
                    step_info.brain_name_to_action_info.get(behavior_id, ActionInfo.empty()),
                )
                manager.record_environment_stats(step_info.environment_stats, step_info.worker_id)
        return len(step_infos)

    def _step(self) -> List[EnvironmentStep]:
        raise NotImplementedError

    def _reset_env(self, config: Optional[Dict] = None) -> List[EnvironmentStep]:
        raise NotImplementedError

    def set_env_parameters(self, config: Optional[Dict] = None) -> None:
        raise NotImplementedError

    def on_training_started(self, behavior_name: str, settings: TrainerSettings) -> None:
        pass

    @property
    def training_behaviors(self) -> Dict[BehaviorName, BehaviorSpec]:
        raise NotImplementedError

    def close(self) -> None:
        raise NotImplementedError


class EnvironmentCommand(enum.Enum):
    STEP = 1
    BEHAVIOR_SPECS = 2
    ENVIRONMENT_PARAMETERS = 3
    RESET = 4
    CLOSE = 5
    ENV_EXITED = 6
    CLOSED = 7
    TRAINING_STARTED = 8


class EnvironmentRequest(NamedTuple):
    cmd: EnvironmentCommand
    payload: Any = None


class EnvironmentResponse(NamedTuple):
    cmd: EnvironmentCommand
    worker_id: int
    payload: Any


class StepResponse(NamedTuple):
    all_step_result: AllStepResult
    timer_root: Optional[TimerNode]
    environment_stats: EnvironmentStats


class UnityEnvWorker:
    def __init__(self, process: Process, worker_id: int, conn: Connection):
        self.process = process
        self.worker_id = worker_id
        self.conn = conn
        self.previous_step = EnvironmentStep.empty(worker_id)
        self.previous_all_action_info = {}
        self.waiting = False
        self.closed = False

    def send(self, cmd: EnvironmentCommand, payload: Any = None) -> None:
        try:
            self.conn.send(EnvironmentRequest(cmd, payload))
        except (BrokenPipeError, EOFError) as exception:
            raise UnityCommunicationException("UnityEnvironment worker: send failed.") from exception

    def recv(self) -> EnvironmentResponse:
        try:
            response = self.conn.recv()
            if response.cmd == EnvironmentCommand.ENV_EXITED:
                raise response.payload
            return response
        except (BrokenPipeError, EOFError) as exception:
            raise UnityCommunicationException("UnityEnvironment worker: recv failed.") from exception

    def request_close(self) -> None:
        try:
            self.conn.send(EnvironmentRequest(EnvironmentCommand.CLOSE))
        except (BrokenPipeError, EOFError):
            logger.debug(f"UnityEnvWorker {self.worker_id} got exception trying to close.")


def worker(
    parent_conn: Connection,
    step_queue: Queue,
    pickled_env_factory: bytes,
    worker_id: int,
    run_options: RunOptions,
    log_level: int = logging_util.INFO,
) -> None:
    env_factory = cloudpickle.loads(pickled_env_factory)
    env_parameters = EnvironmentParametersChannel()
    engine_settings = run_options.engine_settings
    engine_configuration = EngineConfigurationChannel()
    engine_configuration.set_configuration(EngineConfig(
        width=engine_settings.width,
        height=engine_settings.height,
        quality_level=engine_settings.quality_level,
        time_scale=engine_settings.time_scale,
        target_frame_rate=engine_settings.target_frame_rate,
        capture_frame_rate=engine_settings.capture_frame_rate,
    ))
    stats_channel = StatsSideChannel()
    analytics = TrainingAnalyticsSideChannel() if worker_id == 0 else None
    env: Optional[UnityEnvironment] = None
    logging_util.set_log_level(log_level)

    def send_response(cmd: EnvironmentCommand, payload: Any) -> None:
        parent_conn.send(EnvironmentResponse(cmd, worker_id, payload))

    def all_results() -> AllStepResult:
        return {name: env.get_steps(name) for name in env.behavior_specs}

    try:
        side_channels = [env_parameters, engine_configuration, stats_channel]
        if analytics is not None:
            side_channels.append(analytics)
        env = env_factory(worker_id, side_channels)
        if not env.academy_capabilities or not env.academy_capabilities.trainingAnalytics:
            analytics = None
        if analytics is not None:
            analytics.environment_initialized(run_options)

        while True:
            request = parent_conn.recv()
            if request.cmd == EnvironmentCommand.STEP:
                for behavior_name, action_info in request.payload.items():
                    if len(action_info.agent_ids) > 0:
                        env.set_actions(behavior_name, action_info.env_action)
                env.step()
                response = StepResponse(
                    all_results(), get_timer_root(), stats_channel.get_and_reset_stats()
                )
                step_queue.put(EnvironmentResponse(EnvironmentCommand.STEP, worker_id, response))
                reset_timers()
            elif request.cmd == EnvironmentCommand.BEHAVIOR_SPECS:
                send_response(EnvironmentCommand.BEHAVIOR_SPECS, env.behavior_specs)
            elif request.cmd == EnvironmentCommand.ENVIRONMENT_PARAMETERS:
                for key, value in request.payload.items():
                    if isinstance(value, ParameterRandomizationSettings):
                        value.apply(key, env_parameters)
            elif request.cmd == EnvironmentCommand.TRAINING_STARTED:
                if analytics is not None:
                    analytics.training_started(*request.payload)
            elif request.cmd == EnvironmentCommand.RESET:
                env.reset()
                send_response(EnvironmentCommand.RESET, all_results())
            elif request.cmd == EnvironmentCommand.CLOSE:
                break
    except (
        KeyboardInterrupt,
        UnityCommunicationException,
        UnityTimeOutException,
        UnityEnvironmentException,
        UnityCommunicatorStoppedException,
    ) as exception:
        logger.debug(f"UnityEnvironment worker {worker_id}: environment stopping.")
        step_queue.put(EnvironmentResponse(EnvironmentCommand.ENV_EXITED, worker_id, exception))
        send_response(EnvironmentCommand.ENV_EXITED, exception)
    except Exception as exception:
        logger.exception(f"UnityEnvironment worker {worker_id}: unexpected exception.")
        step_queue.put(EnvironmentResponse(EnvironmentCommand.ENV_EXITED, worker_id, exception))
        send_response(EnvironmentCommand.ENV_EXITED, exception)
    finally:
        if env is not None:
            env.close()
        parent_conn.close()
        step_queue.put(EnvironmentResponse(EnvironmentCommand.CLOSED, worker_id, None))
        step_queue.close()


class SubprocessEnvScheduler(EnvironmentScheduler):
    def __init__(
        self,
        env_factory: Callable[[int, List[SideChannel]], BaseEnv],
        run_options: RunOptions,
        n_env: int = 1,
        worker_factory: Optional[Callable] = None,
        microbatch_settings: Optional[MicrobatchSettings] = None,
    ):
        super().__init__()
        self.env_workers = []
        self.step_queue = Queue()
        self.workers_alive = 0
        self.env_factory = env_factory
        self.run_options = run_options
        self.env_parameters = None
        self.recent_restart_timestamps = [[] for _ in range(n_env)]
        self.restart_counts = [0] * n_env
        self.worker_factory = worker_factory or self.create_worker
        self.microbatch = InferenceMicrobatch(
            microbatch_settings or MicrobatchSettings.create(1, n_env)
        )
        for worker_id in range(n_env):
            self.env_workers.append(
                self.worker_factory(worker_id, self.step_queue, env_factory, run_options)
            )
            self.workers_alive += 1

    def set_policy(self, behavior_name: BehaviorName, policy) -> None:
        self.microbatch.register_policy(policy)
        super().set_policy(behavior_name, policy)

    @staticmethod
    def create_worker(worker_id, step_queue, env_factory, run_options) -> UnityEnvWorker:
        parent_conn, child_conn = Pipe()
        child_process = Process(
            target=worker,
            args=(
                child_conn,
                step_queue,
                cloudpickle.dumps(env_factory),
                worker_id,
                run_options,
                logger.level,
            ),
        )
        child_process.start()
        return UnityEnvWorker(child_process, worker_id, parent_conn)

    def _queue_steps(self) -> None:
        ready_workers = [worker for worker in self.env_workers if not worker.waiting]
        actions_by_worker = self.microbatch.actions_for_workers(
            [
                (worker.worker_id, worker.previous_step.current_all_step_result)
                for worker in ready_workers
            ],
            self.policies,
        )
        for env_worker in ready_workers:
            actions = actions_by_worker[env_worker.worker_id]
            env_worker.previous_all_action_info = actions
            env_worker.send(EnvironmentCommand.STEP, actions)
            env_worker.waiting = True

    def _restart_failed_workers(self, first_failure: EnvironmentResponse) -> None:
        if first_failure.cmd != EnvironmentCommand.ENV_EXITED:
            return
        failures = {first_failure.worker_id: first_failure.payload, **self._drain_step_queue()}
        for worker_id, exception in failures.items():
            self._assert_worker_can_restart(worker_id, exception)
            logger.warning(f"Restarting worker[{worker_id}] after '{exception}'")
            self.recent_restart_timestamps[worker_id].append(datetime.datetime.now())
            self.restart_counts[worker_id] += 1
            self.env_workers[worker_id] = self.worker_factory(
                worker_id, self.step_queue, self.env_factory, self.run_options
            )
        self.reset(self.env_parameters)

    def _drain_step_queue(self) -> Dict[int, Exception]:
        failures = {}
        pending = {worker.worker_id for worker in self.env_workers if worker.waiting}
        deadline = datetime.datetime.now() + datetime.timedelta(minutes=1)
        while pending and deadline > datetime.datetime.now():
            try:
                while True:
                    response = self.step_queue.get_nowait()
                    if response.cmd == EnvironmentCommand.ENV_EXITED:
                        pending.add(response.worker_id)
                        failures[response.worker_id] = response.payload
                    else:
                        pending.remove(response.worker_id)
                        self.env_workers[response.worker_id].waiting = False
            except EmptyQueueException:
                pass
        if deadline < datetime.datetime.now():
            waiting = {worker.worker_id for worker in self.env_workers if worker.waiting}
            raise TimeoutError(f"Workers {waiting} stuck in waiting state")
        return failures

    def _assert_worker_can_restart(self, worker_id: int, exception: Exception) -> None:
        recoverable = isinstance(exception, (
            UnityCommunicationException,
            UnityTimeOutException,
            UnityEnvironmentException,
            UnityCommunicatorStoppedException,
        ))
        if recoverable and self._worker_has_restart_quota(worker_id):
            return
        if recoverable:
            logger.error(f"Worker {worker_id} exceeded the allowed number of restarts.")
        raise exception

    def _worker_has_restart_quota(self, worker_id: int) -> bool:
        self._drop_old_restart_timestamps(worker_id)
        settings = self.run_options.env_settings
        lifetime = settings.max_lifetime_restarts
        lifetime_ok = lifetime == -1 or self.restart_counts[worker_id] < lifetime
        rate = settings.restarts_rate_limit_n
        rate_ok = rate == -1 or len(self.recent_restart_timestamps[worker_id]) < rate
        return lifetime_ok and rate_ok

    def _drop_old_restart_timestamps(self, worker_id: int) -> None:
        cutoff = datetime.datetime.now() - datetime.timedelta(
            seconds=self.run_options.env_settings.restarts_rate_limit_period_s
        )
        self.recent_restart_timestamps[worker_id] = [
            stamp for stamp in self.recent_restart_timestamps[worker_id] if stamp > cutoff
        ]

    def _step(self) -> List[EnvironmentStep]:
        self._queue_steps()
        responses = []
        response_workers: Set[int] = set()
        deadline = None
        while True:
            if responses and (
                self.microbatch.settings.effective_worker_cap == 1
                or not any(worker.waiting for worker in self.env_workers)
                or time.perf_counter() >= deadline
            ):
                return self._postprocess_steps(responses)
            try:
                response = self.step_queue.get_nowait()
            except EmptyQueueException:
                continue
            if response.cmd == EnvironmentCommand.ENV_EXITED:
                self._restart_failed_workers(response)
                responses.clear()
                response_workers.clear()
                deadline = None
                self._queue_steps()
                continue
            if response.worker_id in response_workers:
                continue
            self.env_workers[response.worker_id].waiting = False
            responses.append(response)
            response_workers.add(response.worker_id)
            if (
                len(responses) == 1
                and self.microbatch.settings.effective_worker_cap > 1
            ):
                deadline = time.perf_counter() + (
                    self.microbatch.settings.window_micros / 1_000_000
                )
            if (
                len(response_workers) >= self.microbatch.settings.effective_worker_cap
                or not any(worker.waiting for worker in self.env_workers)
            ):
                return self._postprocess_steps(responses)

    def _reset_env(self, config: Optional[Dict] = None) -> List[EnvironmentStep]:
        while any(worker.waiting for worker in self.env_workers):
            if not self.step_queue.empty():
                response = self.step_queue.get_nowait()
                self.env_workers[response.worker_id].waiting = False
        self.set_env_parameters(config)
        for worker in self.env_workers:
            worker.send(EnvironmentCommand.RESET, config)
        for worker in self.env_workers:
            worker.previous_step = EnvironmentStep(worker.recv().payload, worker.worker_id, {}, {})
        return [worker.previous_step for worker in self.env_workers]

    def set_env_parameters(self, config: Optional[Dict] = None) -> None:
        self.env_parameters = config
        for worker in self.env_workers:
            worker.send(EnvironmentCommand.ENVIRONMENT_PARAMETERS, config)

    def on_training_started(self, behavior_name: str, settings: TrainerSettings) -> None:
        for worker in self.env_workers:
            worker.send(EnvironmentCommand.TRAINING_STARTED, (behavior_name, settings))

    @property
    def training_behaviors(self) -> Dict[BehaviorName, BehaviorSpec]:
        result = {}
        for worker in self.env_workers:
            worker.send(EnvironmentCommand.BEHAVIOR_SPECS)
            result.update(worker.recv().payload)
        return result

    def close(self) -> None:
        for worker in self.env_workers:
            worker.request_close()
        deadline = time.time() + WORKER_SHUTDOWN_TIMEOUT_S
        while self.workers_alive > 0 and time.time() < deadline:
            try:
                response = self.step_queue.get_nowait()
                worker = self.env_workers[response.worker_id]
                if response.cmd == EnvironmentCommand.CLOSED and not worker.closed:
                    worker.closed = True
                    self.workers_alive -= 1
            except EmptyQueueException:
                pass
        self.step_queue.close()
        if self.workers_alive > 0:
            logger.error("SubprocessEnvScheduler had workers that didn't signal shutdown")
            for worker in self.env_workers:
                if not worker.closed and worker.process.is_alive():
                    worker.process.terminate()
                    logger.error(f"Worker {worker.worker_id} was forcefully terminated.")
        self.step_queue.join_thread()

    def _postprocess_steps(self, responses: List[EnvironmentResponse]) -> List[EnvironmentStep]:
        step_infos = []
        timer_nodes = []
        for response in responses:
            payload = response.payload
            worker = self.env_workers[response.worker_id]
            step = EnvironmentStep(
                payload.all_step_result,
                response.worker_id,
                worker.previous_all_action_info,
                payload.environment_stats,
            )
            step_infos.append(step)
            worker.previous_step = step
            if payload.timer_root:
                timer_nodes.append(payload.timer_root)
        if timer_nodes:
            with hierarchical_timer("workers") as main_timer:
                for timer_node in timer_nodes:
                    main_timer.merge(timer_node, root_name="worker_root", is_parallel=True)
        return step_infos

    @timed
    def _take_step(self, last_step: EnvironmentStep) -> Dict[BehaviorName, ActionInfo]:
        actions = {}
        for behavior_name, step_tuple in last_step.current_all_step_result.items():
            if behavior_name in self.policies:
                actions[behavior_name] = self.policies[behavior_name].get_action(
                    step_tuple[0], last_step.worker_id
                )
        return actions
