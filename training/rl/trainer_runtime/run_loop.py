import json
import os
from collections import defaultdict
from datetime import datetime
from pathlib import Path
from typing import Dict, Optional, Set

import mlagents.trainers
import mlagents_envs
import numpy as np
import yaml
from mlagents import torch_utils
from mlagents.plugins.stats_writer import register_stats_writer_plugins
from mlagents.trainers.agent_processor import AgentManager
from mlagents.trainers.behavior_id_utils import BehaviorIdentifiers
from mlagents.trainers.directory_utils import setup_init_path, validate_existing_directories
from mlagents.trainers.environment_parameter_manager import EnvironmentParameterManager
from mlagents.trainers.settings import RunOptions
from mlagents.trainers.stats import StatsReporter
from mlagents.trainers.trainer import TrainerFactory
from mlagents.trainers.training_status import GlobalTrainingStatus
from mlagents.torch_utils.globals import get_rank
from mlagents_envs import logging_util
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.exception import (
    UnityCommunicationException,
    UnityCommunicatorStoppedException,
    UnityEnvironmentException,
)
from mlagents_envs.timers import add_metadata, get_timer_tree, hierarchical_timer, timed

from trainer_runtime.contract import MANIFEST_NAME, RunManifest, config_sha256, summaries_path, write_manifest
from trainer_runtime.env_scheduler import EnvironmentScheduler, SubprocessEnvScheduler
from trainer_runtime.publish import AtomicTorchModelSaver, CheckpointCommitter, atomic_write_training_status
from trainer_runtime.stats_writer import JsonlStatsWriter

logger = logging_util.get_logger(__name__)


def validate_owned_options(options: RunOptions) -> None:
    if len(options.behaviors) != 1:
        raise RuntimeError(
            f"owned trainer entry requires exactly one behavior; found {len(options.behaviors)}"
        )
    threaded = [name for name, settings in options.behaviors.items() if settings.threaded]
    if threaded:
        raise RuntimeError(f"owned trainer entry does not support threaded trainers: {threaded}")


def owned_stats_writers(
    options: RunOptions, config_path: Path, started_at: datetime
) -> list[JsonlStatsWriter]:
    behavior, trainer_settings = next(iter(options.behaviors.items()))
    checkpoint = options.checkpoint_settings
    results_dir = Path(checkpoint.results_dir).resolve()
    manifest = RunManifest(
        run_id=checkpoint.run_id,
        behavior=behavior,
        results_dir=results_dir,
        started_at=started_at,
        max_steps=trainer_settings.max_steps,
        mode=_mode(trainer_settings.self_play is not None),
        config_hash=config_sha256(config_path),
    )
    write_manifest(manifest.run_dir / MANIFEST_NAME, manifest)
    return [JsonlStatsWriter(
        summaries_path(manifest.run_dir), behavior, manifest.max_steps, checkpoint.resume
    )]


def run_cli(options: RunOptions, config_path: Path, started_at: datetime) -> None:
    validate_owned_options(options)
    print(_version_string())
    log_level = logging_util.DEBUG if options.debug else logging_util.INFO
    logging_util.set_log_level(log_level)
    logger.debug(json.dumps(options.as_dict(), indent=4))
    if options.checkpoint_settings.load_model:
        logger.warning("The --load option is deprecated. Use --resume instead.")
    if options.checkpoint_settings.train_model:
        logger.warning("The --train option is deprecated. Training is already the default.")

    run_seed = options.env_settings.seed
    if run_seed == -1:
        run_seed = np.random.randint(0, 10000)
        logger.debug(f"run_seed set to {run_seed}")
    add_metadata("mlagents_version", mlagents.trainers.__version__)
    add_metadata("mlagents_envs_version", mlagents_envs.__version__)
    add_metadata("communication_protocol_version", UnityEnvironment.API_VERSION)
    add_metadata("pytorch_version", torch_utils.torch.__version__)
    add_metadata("numpy_version", np.__version__)
    run_training(run_seed, options, config_path, started_at)


def run_training(
    run_seed: int, options: RunOptions, config_path: Path, started_at: datetime
) -> None:
    with hierarchical_timer("run_training.setup"):
        torch_utils.set_torch_config(options.torch_settings)
        checkpoint = options.checkpoint_settings
        env_settings = options.env_settings
        engine_settings = options.engine_settings
        run_logs_dir = Path(checkpoint.run_logs_dir)
        validate_existing_directories(
            checkpoint.write_path,
            checkpoint.resume,
            checkpoint.force,
            checkpoint.maybe_init_path,
        )
        run_logs_dir.mkdir(parents=True, exist_ok=True)
        if checkpoint.resume:
            GlobalTrainingStatus.load_state(str(run_logs_dir / "training_status.json"))
        elif checkpoint.maybe_init_path is not None:
            setup_init_path(options.behaviors, checkpoint.maybe_init_path)

        writers = register_stats_writer_plugins(options)
        writers.extend(owned_stats_writers(options, config_path, started_at))
        for writer in writers:
            StatsReporter.add_writer(writer)

        port = None if env_settings.env_path is None else env_settings.base_port
        env_factory = create_environment_factory(
            env_settings.env_path,
            engine_settings.no_graphics,
            engine_settings.no_graphics_monitor,
            run_seed,
            env_settings.num_areas,
            env_settings.timeout_wait,
            port,
            env_settings.env_args,
            str(run_logs_dir.resolve()),
        )
        scheduler = SubprocessEnvScheduler(env_factory, options, env_settings.num_envs)
        parameters = EnvironmentParameterManager(
            options.environment_parameters, run_seed, restore=checkpoint.resume
        )
        factory = TrainerFactory(
            trainer_config=options.behaviors,
            output_path=checkpoint.write_path,
            train_model=not checkpoint.inference,
            load_model=checkpoint.resume,
            seed=run_seed,
            param_manager=parameters,
            init_path=checkpoint.maybe_init_path,
            multi_gpu=False,
        )
        loop = OwnedRunLoop(
            factory,
            checkpoint.write_path,
            checkpoint.run_id,
            parameters,
            not checkpoint.inference,
            run_seed,
            run_logs_dir,
            checkpoint.resume,
        )

    try:
        loop.start_learning(scheduler)
    finally:
        scheduler.close()
        write_run_options(Path(checkpoint.write_path), options)
        write_timing_tree(run_logs_dir)
        atomic_write_training_status(run_logs_dir / "training_status.json")


def create_environment_factory(
    env_path: Optional[str],
    no_graphics: bool,
    no_graphics_monitor: bool,
    seed: int,
    num_areas: int,
    timeout_wait: int,
    start_port: Optional[int],
    env_args,
    log_folder: str,
):
    forwarded_args = list(env_args or [])

    def create_unity_environment(worker_id: int, side_channels):
        additional_args = forwarded_args + ["--harness-worker-index", str(worker_id)]
        return UnityEnvironment(
            file_name=env_path,
            worker_id=worker_id,
            seed=seed + worker_id,
            num_areas=num_areas,
            no_graphics=no_graphics,
            no_graphics_monitor=no_graphics_monitor,
            base_port=start_port,
            additional_args=additional_args,
            side_channels=side_channels,
            log_folder=log_folder,
            timeout_wait=timeout_wait,
        )

    return create_unity_environment


class OwnedRunLoop:
    def __init__(
        self,
        trainer_factory: TrainerFactory,
        output_path: str,
        run_id: str,
        parameter_manager: EnvironmentParameterManager,
        train: bool,
        training_seed: int,
        run_logs_dir: Path,
        resume: bool,
    ):
        self.trainers = {}
        self.brain_name_to_identifier: Dict[str, Set] = defaultdict(set)
        self.trainer_factory = trainer_factory
        self.output_path = output_path
        self.run_id = run_id
        self.train_model = train
        self.param_manager = parameter_manager
        self.ghost_controller = trainer_factory.ghost_controller
        self.registered_behavior_ids = set()
        self.savers = {}
        self.committer = CheckpointCommitter(
            Path(output_path), run_logs_dir / "training_status.json", resume
        )
        np.random.seed(training_seed)
        torch_utils.torch.manual_seed(training_seed)
        self.rank = get_rank()

    @timed
    def _save_models(self) -> None:
        if self.rank is not None and self.rank != 0:
            return
        for trainer in self.trainers.values():
            trainer.save_model()
        self._commit_pending()
        for saver in self.savers.values():
            saver.publish_final_models()
        logger.debug("Saved Model")

    def _reset_env(self, scheduler: EnvironmentScheduler) -> None:
        scheduler.reset(config=self.param_manager.get_current_samplers())
        self._register_new_behaviors(scheduler, scheduler.first_step_infos)

    def _not_done_training(self) -> bool:
        return (
            any(trainer.should_still_train for trainer in self.trainers.values())
            or not self.train_model
            or not self.trainers
        )

    def _create_trainer_and_manager(
        self, scheduler: EnvironmentScheduler, behavior_id: str
    ) -> None:
        parsed = BehaviorIdentifiers.from_name_behavior_id(behavior_id)
        brain_name = parsed.brain_name
        if brain_name in self.trainers:
            trainer = self.trainers[brain_name]
        else:
            trainer = self.trainer_factory.generate(brain_name)
            if trainer.threaded:
                raise RuntimeError(f"owned trainer entry does not support threaded trainer {brain_name}")
            self._inject_saver(brain_name, trainer)
            self.trainers[brain_name] = trainer
            scheduler.on_training_started(
                brain_name, self.trainer_factory.trainer_config[brain_name]
            )

        policy = trainer.create_policy(parsed, scheduler.training_behaviors[behavior_id])
        trainer.add_policy(parsed, policy)
        manager = AgentManager(
            policy,
            behavior_id,
            trainer.stats_reporter,
            trainer.parameters.time_horizon,
            threaded=False,
        )
        scheduler.set_agent_manager(behavior_id, manager)
        scheduler.set_policy(behavior_id, policy)
        self.brain_name_to_identifier[brain_name].add(behavior_id)
        trainer.publish_policy_queue(manager.policy_queue)
        trainer.subscribe_trajectory_queue(manager.trajectory_queue)

    def _inject_saver(self, brain_name: str, trainer) -> None:
        inner = getattr(trainer, "trainer", trainer)
        saver = AtomicTorchModelSaver(
            inner.trainer_settings, inner.artifact_path, inner.load
        )
        inner.model_saver = saver
        trainer.model_saver = saver
        self.savers[brain_name] = saver

    @timed
    def start_learning(self, scheduler: EnvironmentScheduler) -> None:
        Path(self.output_path).mkdir(parents=True, exist_ok=True)
        try:
            self._reset_env(scheduler)
            self.param_manager.log_current_lesson()
            while self._not_done_training():
                processed = self.advance(scheduler)
                for _ in range(processed):
                    self.reset_env_if_ready(scheduler)
        except (
            KeyboardInterrupt,
            UnityCommunicationException,
            UnityEnvironmentException,
            UnityCommunicatorStoppedException,
        ) as exception:
            logger.info("Learning was interrupted. Please wait while the graph is generated.")
            if not isinstance(exception, (KeyboardInterrupt, UnityCommunicatorStoppedException)):
                raise
        finally:
            if self.train_model:
                self._save_models()

    def end_trainer_episodes(self) -> None:
        for trainer in self.trainers.values():
            trainer.end_episode()

    def reset_env_if_ready(self, scheduler: EnvironmentScheduler) -> None:
        rewards = {name: list(trainer.reward_buffer) for name, trainer in self.trainers.items()}
        steps = {name: int(trainer.get_step) for name, trainer in self.trainers.items()}
        max_steps = {name: int(trainer.get_max_steps) for name, trainer in self.trainers.items()}
        updated, must_reset = self.param_manager.update_lessons(steps, max_steps, rewards)
        if updated:
            for trainer in self.trainers.values():
                trainer.reward_buffer.clear()
        if must_reset or self.ghost_controller.should_reset():
            self._reset_env(scheduler)
            self.end_trainer_episodes()
        elif updated:
            scheduler.set_env_parameters(self.param_manager.get_current_samplers())

    @timed
    def advance(self, scheduler: EnvironmentScheduler) -> int:
        with hierarchical_timer("env_step"):
            step_infos = scheduler.get_steps()
            self._register_new_behaviors(scheduler, step_infos)
            processed = scheduler.process_steps(step_infos)
        for parameter, lesson in self.param_manager.get_current_lesson_number().items():
            for trainer in self.trainers.values():
                trainer.stats_reporter.set_stat(
                    f"Environment/Lesson Number/{parameter}", lesson
                )
        for trainer in self.trainers.values():
            with hierarchical_timer("trainer_advance"):
                trainer.advance()
        self._commit_pending()
        return processed

    def _commit_pending(self) -> None:
        for brain_name, saver in self.savers.items():
            self.committer.commit(saver, self.trainers[brain_name])

    def _register_new_behaviors(
        self, scheduler: EnvironmentScheduler, step_infos
    ) -> None:
        seen = set()
        for step in step_infos:
            seen.update(step.name_behavior_ids)
        for behavior_id in seen - self.registered_behavior_ids:
            self._create_trainer_and_manager(scheduler, behavior_id)
        self.registered_behavior_ids.update(seen)


def write_run_options(output_dir: Path, options: RunOptions) -> None:
    try:
        with (output_dir / "configuration.yaml").open("w", encoding="utf-8") as handle:
            try:
                yaml.dump(options.as_dict(), handle, sort_keys=False)
            except TypeError:
                yaml.dump(options.as_dict(), handle)
    except FileNotFoundError:
        logger.warning(f"Unable to save configuration under {output_dir}")


def write_timing_tree(output_dir: Path) -> None:
    try:
        with (output_dir / "timers.json").open("w", encoding="utf-8") as handle:
            json.dump(get_timer_tree(), handle, indent=4)
    except FileNotFoundError:
        logger.warning(f"Unable to save timers under {output_dir}")


def _mode(self_play: bool) -> str:
    if os.environ.get("RL_HYBRID_SCRIPTED_WORKERS") is not None:
        return "hybrid"
    return "self-play" if self_play else "scripted"


def _version_string() -> str:
    return (
        "Version information:\n"
        f"  ml-agents: {mlagents.trainers.__version__},\n"
        f"  ml-agents-envs: {mlagents_envs.__version__},\n"
        f"  Communicator API: {UnityEnvironment.API_VERSION},\n"
        f"  PyTorch: {torch_utils.torch.__version__}"
    )
