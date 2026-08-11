"""Train and export an inspectable value baseline from executed transitions."""

from __future__ import annotations

import copy
import hashlib
import json
import math
import platform
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence

import numpy as np


TRANSITION_SCHEMA = "rl-transition-v1"
STATE_SCHEMA = "rl-value-combat-v1"
ARTIFACT_SCHEMA = "rl-value-artifact-v1"
SPLIT_SALT = "rl-value-split-v1"
GAMMA = 0.99

FEATURE_NAMES = (
    "selfVelocityX",
    "selfVelocityY",
    "selfSpeedPct",
    "selfYawRatePct",
    "selfHealthPct",
    "selfShieldPct",
    "selfBoostAvailable",
    "selfBoostCooldownPct",
    "hasTarget",
    "targetRelPositionX",
    "targetRelPositionY",
    "targetDistance",
    "targetRelVelocityX",
    "targetRelVelocityY",
    "targetFacingX",
    "targetFacingY",
    "targetHealthPct",
    "targetShieldPct",
    "inMyEnvelope",
    "inEnemyEnvelope",
    "arenaCenterX",
    "arenaCenterY",
    "primaryWeaponReady",
    "primaryHeatPct",
    "interceptLeadX",
    "interceptLeadY",
    "enemyWeaponReady",
    "enemyHeatPct",
)

REWARD_FIELDS = (
    "dense",
    "shapingEnvelope",
    "shapingBorder",
    "timeCost",
    "outcome",
)


class ValueBaselineError(RuntimeError):
    pass


@dataclass(frozen=True, order=True)
class EpisodeKey:
    run_id: str
    worker_index: int
    arena_index: int
    run_seed: int
    episode_index: int
    team_id: int

    @property
    def stable_id(self) -> str:
        return (
            f"{self.run_id}/w{self.worker_index}/a{self.arena_index}/"
            f"s{self.run_seed}/e{self.episode_index}/t{self.team_id}"
        )

    def as_dict(self) -> dict[str, Any]:
        return {
            "runId": self.run_id,
            "workerIndex": self.worker_index,
            "arenaIndex": self.arena_index,
            "runSeed": self.run_seed,
            "episodeIndex": self.episode_index,
            "teamId": self.team_id,
            "episodeId": self.stable_id,
        }


@dataclass
class SourceRow:
    data: dict[str, Any]
    path: Path
    line: int

    @property
    def decision(self) -> int:
        return int(self.data["decision"])

    @property
    def where(self) -> str:
        return f"{self.path}:{self.line}"


@dataclass
class Episode:
    key: EpisodeKey
    rows: list[SourceRow]
    end_kind: str


@dataclass(frozen=True)
class TrainingConfig:
    split_counts: tuple[int, int, int] = (8, 2, 2)
    min_terminal_episodes_per_seed: int = 10
    hidden_units: int = 64
    learning_rate: float = 1e-3
    batch_size: int = 1024
    max_epochs: int = 200
    patience: int = 20
    random_seed: int = 365
    inference_tolerance: float = 1e-5


@dataclass(frozen=True)
class ArtifactMetadata:
    artifact_id: str
    collection_command: str
    collection_config: Path | None = None
    source_commit: str | None = None


@dataclass
class PreparedData:
    features: np.ndarray
    task_returns: np.ndarray
    shaping_envelope_returns: np.ndarray
    shaping_border_returns: np.ndarray
    episode_ids: np.ndarray
    run_seeds: np.ndarray
    decisions: np.ndarray
    keys: list[EpisodeKey]
    sources: np.ndarray
    split_by_seed: dict[int, str]
    episode_audit: list[dict[str, Any]]

    def indices(self, split: str) -> np.ndarray:
        return np.flatnonzero(np.asarray([self.split_by_seed[int(seed)] == split for seed in self.run_seeds]))


def discover_transition_files(inputs: Sequence[Path]) -> list[Path]:
    files: set[Path] = set()
    for value in inputs:
        path = value.resolve()
        if path.is_dir():
            files.update(item.resolve() for item in path.rglob("*-transitions.jsonl") if item.is_file())
        elif path.is_file():
            files.add(path)
        else:
            raise ValueBaselineError(f"transition input does not exist: {path}")
    if not files:
        raise ValueBaselineError("no transition JSONL files were found")
    return sorted(files)


def load_episodes(files: Sequence[Path]) -> list[Episode]:
    grouped: dict[EpisodeKey, dict[int, SourceRow]] = {}
    episode_sources: dict[EpisodeKey, Path] = {}
    collection_run_id: str | None = None
    for path in files:
        with path.open("r", encoding="utf-8") as stream:
            for line_number, text in enumerate(stream, 1):
                if not text.strip():
                    continue
                try:
                    row = json.loads(text)
                except json.JSONDecodeError as error:
                    raise ValueBaselineError(f"{path}:{line_number}: invalid JSON: {error.msg}") from error
                if not isinstance(row, dict):
                    raise ValueBaselineError(f"{path}:{line_number}: transition row must be an object")
                source = SourceRow(row, path, line_number)
                key = validate_transition(source)
                if collection_run_id is None:
                    collection_run_id = key.run_id
                elif key.run_id != collection_run_id:
                    raise ValueBaselineError(
                        f"{source.where}: runId {key.run_id!r} does not match "
                        f"collection runId {collection_run_id!r}"
                    )
                prior_path = episode_sources.setdefault(key, path)
                if prior_path != path:
                    raise ValueBaselineError(
                        f"{source.where}: duplicate episode identity {key.stable_id}; "
                        f"first seen in {prior_path}"
                    )
                decisions = grouped.setdefault(key, {})
                if source.decision in decisions:
                    prior = decisions[source.decision]
                    raise ValueBaselineError(
                        f"{source.where}: duplicate {key.stable_id} decision {source.decision}; "
                        f"first seen at {prior.where}"
                    )
                decisions[source.decision] = source

    last_episode_by_stream: dict[tuple[str, int, int, int, int], int] = {}
    for key in grouped:
        stream = (key.run_id, key.worker_index, key.arena_index, key.run_seed, key.team_id)
        last_episode_by_stream[stream] = max(last_episode_by_stream.get(stream, -1), key.episode_index)

    episodes = []
    for key, by_decision in sorted(grouped.items()):
        rows = [by_decision[index] for index in sorted(by_decision)]
        stream = (key.run_id, key.worker_index, key.arena_index, key.run_seed, key.team_id)
        episodes.append(validate_episode(
            key, rows, allow_collection_end=key.episode_index == last_episode_by_stream[stream]
        ))
    if not episodes:
        raise ValueBaselineError("transition inputs contained no rows")
    return episodes


def validate_transition(source: SourceRow) -> EpisodeKey:
    row = source.data
    require(row.get("schema") == TRANSITION_SCHEMA, source, f"schema must be {TRANSITION_SCHEMA}")
    require(row.get("observationSize") == len(FEATURE_NAMES), source, "observationSize must be 28")
    require(row.get("obstacleTokenCap") == 64, source, "obstacleTokenCap must be 64")
    require(row.get("obstacleTokenFloats") == 7, source, "obstacleTokenFloats must be 7")
    require(row.get("continuousActionSize") == 5, source, "continuousActionSize must be 5")
    require(row.get("discreteActionBranches") == [2, 2], source, "discreteActionBranches must be [2, 2]")
    require(row.get("rewardFields") == list(REWARD_FIELDS), source, "rewardFields do not match v1")

    run_id = row.get("runId")
    require(isinstance(run_id, str) and run_id.strip(), source, "runId must be non-empty")
    worker = require_int(row, "workerIndex", source, minimum=0)
    arena = require_int(row, "arenaIndex", source, minimum=0)
    run_seed = require_int(row, "runSeed", source)
    episode = require_int(row, "episodeIndex", source, minimum=0)
    team = require_int(row, "teamId", source, allowed={0, 1})
    require_int(row, "decision", source, minimum=1)

    validate_observation(row.get("state"), source, "state")
    validate_observation(row.get("nextState"), source, "nextState")
    validate_action(row.get("action"), source)
    validate_reward(row.get("reward"), source)
    terminal = row.get("terminal")
    truncated = row.get("truncated")
    require(type(terminal) is bool, source, "terminal must be boolean")
    require(type(truncated) is bool, source, "truncated must be boolean")
    require(not (terminal and truncated), source, "transition cannot be terminal and truncated")

    return EpisodeKey(run_id, worker, arena, run_seed, episode, team)


def validate_episode(key: EpisodeKey, rows: list[SourceRow], allow_collection_end: bool) -> Episode:
    for expected, row in enumerate(rows, 1):
        if row.decision != expected:
            raise ValueBaselineError(
                f"{row.where}: {key.stable_id} decisions must be contiguous from 1; "
                f"expected {expected}, found {row.decision}"
            )

    for index, row in enumerate(rows[:-1]):
        require(not row.data["terminal"] and not row.data["truncated"], row,
                f"{key.stable_id} end marker may appear only on the final decision")
        following = rows[index + 1]
        if row.data["nextState"] != following.data["state"]:
            raise ValueBaselineError(
                f"{following.where}: {key.stable_id} decision {following.decision} state "
                f"does not equal decision {row.decision} nextState"
            )

    final = rows[-1]
    if final.data["terminal"]:
        end_kind = "terminal"
    elif final.data["truncated"]:
        end_kind = "truncated"
    elif allow_collection_end:
        end_kind = "collection_end"
    else:
        raise ValueBaselineError(
            f"{final.where}: {key.stable_id} final decision must declare terminal or truncation; "
            "only the final episode in a stream may be censored by collection end"
        )
    return Episode(key, rows, end_kind)


def validate_observation(value: Any, source: SourceRow, name: str) -> None:
    require(isinstance(value, dict), source, f"{name} must be an object")
    combat = value.get("combat")
    obstacles = value.get("obstacleTokens")
    require(isinstance(combat, list) and len(combat) == len(FEATURE_NAMES), source,
            f"{name}.combat must contain 28 values")
    require(isinstance(obstacles, list), source, f"{name}.obstacleTokens must be an array")
    require(len(obstacles) % 7 == 0 and len(obstacles) <= 64 * 7, source,
            f"{name}.obstacleTokens must contain at most 64 whole tokens")
    require_finite(combat, source, f"{name}.combat")
    require_finite(obstacles, source, f"{name}.obstacleTokens")


def validate_action(value: Any, source: SourceRow) -> None:
    require(isinstance(value, dict), source, "action must be an object")
    continuous = value.get("continuous")
    discrete = value.get("discrete")
    require(isinstance(continuous, list) and len(continuous) == 5, source,
            "action.continuous must contain 5 values")
    require(isinstance(discrete, list) and len(discrete) == 2, source,
            "action.discrete must contain 2 values")
    require(all(type(item) is int and item in (0, 1) for item in discrete), source,
            "action.discrete values must be 0 or 1")
    require(type(value.get("boostExecuted")) is bool, source, "action.boostExecuted must be boolean")
    require_finite(continuous, source, "action.continuous")


def validate_reward(value: Any, source: SourceRow) -> None:
    require(isinstance(value, dict), source, "reward must be an object")
    numbers = []
    for name in (*REWARD_FIELDS, "total"):
        item = value.get(name)
        require(is_number(item) and math.isfinite(float(item)), source, f"reward.{name} must be finite")
        numbers.append(float(item))
    expected = sum(numbers[:-1])
    require(math.isclose(numbers[-1], expected, rel_tol=1e-6, abs_tol=1e-6), source,
            f"reward.total {numbers[-1]} does not equal components {expected}")


def require_int(row: dict[str, Any], name: str, source: SourceRow,
                minimum: int | None = None, allowed: set[int] | None = None) -> int:
    value = row.get(name)
    require(type(value) is int, source, f"{name} must be an integer")
    if minimum is not None:
        require(value >= minimum, source, f"{name} must be >= {minimum}")
    if allowed is not None:
        require(value in allowed, source, f"{name} must be one of {sorted(allowed)}")
    return value


def require_finite(values: Iterable[Any], source: SourceRow, name: str) -> None:
    for index, value in enumerate(values):
        require(is_number(value) and math.isfinite(float(value)), source,
                f"{name}[{index}] must be finite")


def is_number(value: Any) -> bool:
    return type(value) in (int, float)


def require(condition: bool, source: SourceRow, message: str) -> None:
    if not condition:
        raise ValueBaselineError(f"{source.where}: {message}")


def assign_seed_splits(seeds: Iterable[int], counts: tuple[int, int, int],
                       salt: str = SPLIT_SALT) -> dict[int, str]:
    unique = sorted(set(int(seed) for seed in seeds))
    if sum(counts) != len(unique) or any(count < 1 for count in counts):
        raise ValueBaselineError(
            f"split counts {counts} require {sum(counts)} distinct seeds; found {len(unique)}"
        )
    ranked = sorted(unique, key=lambda seed: hashlib.sha256(f"{salt}:{seed}".encode()).hexdigest())
    train_count, validation_count, _ = counts
    result = {}
    for index, seed in enumerate(ranked):
        if index < train_count:
            split = "train"
        elif index < train_count + validation_count:
            split = "validation"
        else:
            split = "heldout"
        result[seed] = split
    return result


def discounted_returns(rewards: Sequence[float], gamma: float = GAMMA) -> np.ndarray:
    result = np.empty(len(rewards), dtype=np.float32)
    running = 0.0
    for index in range(len(rewards) - 1, -1, -1):
        running = float(rewards[index]) + gamma * running
        result[index] = running
    return result


def prepare_data(episodes: Sequence[Episode], config: TrainingConfig) -> PreparedData:
    split_by_seed = assign_seed_splits((episode.key.run_seed for episode in episodes), config.split_counts)
    end_counts = end_counts_by_seed(episodes)
    terminal_counts = {seed: 0 for seed in split_by_seed}
    audit = []
    features = []
    task_returns = []
    envelope_returns = []
    border_returns = []
    episode_ids = []
    run_seeds = []
    decisions = []
    keys = []
    sources = []

    for episode in episodes:
        split = split_by_seed[episode.key.run_seed]
        audit_row = episode.key.as_dict() | {
            "split": split,
            "endKind": episode.end_kind,
            "transitions": len(episode.rows),
            "sourceFiles": sorted(set(str(row.path) for row in episode.rows)),
            "sourceLines": [episode.rows[0].line, episode.rows[-1].line],
            "seedTerminalEpisodes": end_counts[episode.key.run_seed]["terminal"],
            "seedTruncatedEpisodes": end_counts[episode.key.run_seed]["truncated"],
            "seedCollectionEndEpisodes": end_counts[episode.key.run_seed]["collection_end"],
        }
        if episode.end_kind != "terminal":
            if episode.end_kind == "truncated":
                label_status = "censored_truncation"
                exclusion_reason = "unknown continuation"
            else:
                label_status = "censored_collection_end"
                exclusion_reason = "collection ended before an explicit episode marker"
            audit_row |= {"labelStatus": label_status, "exclusionReason": exclusion_reason}
            audit.append(audit_row)
            continue

        terminal_counts[episode.key.run_seed] += 1
        rewards = [
            float(row.data["reward"]["dense"])
            + float(row.data["reward"]["timeCost"])
            + float(row.data["reward"]["outcome"])
            for row in episode.rows
        ]
        envelope = [float(row.data["reward"]["shapingEnvelope"]) for row in episode.rows]
        border = [float(row.data["reward"]["shapingBorder"]) for row in episode.rows]
        task = discounted_returns(rewards)
        envelope_rtg = discounted_returns(envelope)
        border_rtg = discounted_returns(border)
        audit_row |= {
            "labelStatus": "labeled_terminal",
            "exclusionReason": None,
            "taskReturnFirst": float(task[0]),
            "taskReturnLast": float(task[-1]),
            "taskReturnMin": float(task.min()),
            "taskReturnMax": float(task.max()),
            "shapingEnvelopeReturnFirst": float(envelope_rtg[0]),
            "shapingBorderReturnFirst": float(border_rtg[0]),
        }
        audit.append(audit_row)

        for index, row in enumerate(episode.rows):
            features.append(row.data["state"]["combat"])
            task_returns.append(task[index])
            envelope_returns.append(envelope_rtg[index])
            border_returns.append(border_rtg[index])
            episode_ids.append(episode.key.stable_id)
            run_seeds.append(episode.key.run_seed)
            decisions.append(row.decision)
            keys.append(episode.key)
            sources.append(row.where)

    inadequate = {
        seed: count for seed, count in terminal_counts.items()
        if count < config.min_terminal_episodes_per_seed
    }
    if inadequate:
        details = ", ".join(
            f"seed {seed} ({split_by_seed[seed]}): {count}"
            for seed, count in sorted(terminal_counts.items())
        )
        raise ValueBaselineError(
            f"terminal-episode adequacy gate requires {config.min_terminal_episodes_per_seed} "
            f"per seed; {details}"
        )

    return PreparedData(
        np.asarray(features, dtype=np.float32),
        np.asarray(task_returns, dtype=np.float32),
        np.asarray(envelope_returns, dtype=np.float32),
        np.asarray(border_returns, dtype=np.float32),
        np.asarray(episode_ids),
        np.asarray(run_seeds, dtype=np.int64),
        np.asarray(decisions, dtype=np.int32),
        keys,
        np.asarray(sources),
        split_by_seed,
        audit,
    )


def build_value_artifact(inputs: Sequence[Path], output_dir: Path, metadata: ArtifactMetadata,
                         config: TrainingConfig = TrainingConfig()) -> dict[str, Any]:
    if output_dir.exists() and any(output_dir.iterdir()):
        raise ValueBaselineError(f"output directory must be new or empty: {output_dir.resolve()}")
    if metadata.collection_config is not None and not metadata.collection_config.is_file():
        raise ValueBaselineError(f"collection config does not exist: {metadata.collection_config.resolve()}")
    files = discover_transition_files(inputs)
    episodes = load_episodes(files)
    split_by_seed = assign_seed_splits((episode.key.run_seed for episode in episodes), config.split_counts)
    output_dir.mkdir(parents=True, exist_ok=True)
    preliminary_audit = audit_episodes(episodes, split_by_seed)
    write_jsonl(output_dir / "episode_audit.jsonl", preliminary_audit)

    data = prepare_data(episodes, config)
    write_jsonl(output_dir / "episode_audit.jsonl", data.episode_audit)

    train = data.indices("train")
    validation = data.indices("validation")
    heldout = data.indices("heldout")
    normalization = fit_normalization(data.features[train], data.task_returns[train])
    trained = train_network(data, train, validation, normalization, config)
    baseline = fit_baselines(data.features[train], data.task_returns[train], normalization)

    predictions = {
        "neural": predict_network(trained["model"], data.features[heldout]),
        "constant": np.full(len(heldout), baseline["constant"], dtype=np.float32),
        "linear": predict_linear(data.features[heldout], baseline),
    }
    metrics = build_metrics(data, heldout, predictions)
    metrics["comparison"] = comparison(metrics)
    metrics["returnSummaries"] = return_summaries(data)

    write_json(output_dir / "metrics.json", metrics)
    write_json(output_dir / "baselines.json", baseline_for_json(baseline))
    write_jsonl(output_dir / "training_history.jsonl", trained["history"])
    write_heldout_predictions(output_dir / "heldout_predictions.jsonl", data, heldout, predictions)
    if metadata.collection_config is not None:
        shutil.copyfile(metadata.collection_config, output_dir / "collection_config.yaml")

    model_path = output_dir / "value.onnx"
    export_onnx(trained["model"], model_path)
    verification = verify_onnx(trained["model"], model_path, data.features[heldout], config.inference_tolerance)
    write_json(output_dir / "verification.json", verification)

    manifest = build_manifest(
        files, output_dir, metadata, config, data, normalization, trained, verification
    )
    write_json(output_dir / "manifest.json", manifest)
    return {"manifest": manifest, "metrics": metrics, "outputDir": str(output_dir.resolve())}


def audit_episodes(episodes: Sequence[Episode], split_by_seed: dict[int, str]) -> list[dict[str, Any]]:
    end_counts = end_counts_by_seed(episodes)
    rows = []
    for episode in episodes:
        rows.append(episode.key.as_dict() | {
            "split": split_by_seed[episode.key.run_seed],
            "endKind": episode.end_kind,
            "transitions": len(episode.rows),
            "sourceFiles": sorted(set(str(row.path) for row in episode.rows)),
            "sourceLines": [episode.rows[0].line, episode.rows[-1].line],
            "seedTerminalEpisodes": end_counts[episode.key.run_seed]["terminal"],
            "seedTruncatedEpisodes": end_counts[episode.key.run_seed]["truncated"],
            "seedCollectionEndEpisodes": end_counts[episode.key.run_seed]["collection_end"],
            "labelStatus": {
                "terminal": "pending_terminal_return",
                "truncated": "censored_truncation",
                "collection_end": "censored_collection_end",
            }[episode.end_kind],
            "exclusionReason": {
                "terminal": None,
                "truncated": "unknown continuation",
                "collection_end": "collection ended before an explicit episode marker",
            }[episode.end_kind],
        })
    return rows


def end_counts_by_seed(episodes: Sequence[Episode]) -> dict[int, dict[str, int]]:
    result: dict[int, dict[str, int]] = {}
    for episode in episodes:
        block = result.setdefault(
            episode.key.run_seed, {"terminal": 0, "truncated": 0, "collection_end": 0}
        )
        block[episode.end_kind] += 1
    return result


def fit_normalization(features: np.ndarray, targets: np.ndarray) -> dict[str, Any]:
    input_mean = features.mean(axis=0, dtype=np.float64).astype(np.float32)
    input_std_raw = features.std(axis=0, dtype=np.float64).astype(np.float32)
    constant_features = [FEATURE_NAMES[index] for index, value in enumerate(input_std_raw) if value < 1e-8]
    input_std = np.where(input_std_raw < 1e-8, 1.0, input_std_raw).astype(np.float32)
    target_mean = float(targets.mean(dtype=np.float64))
    target_std_raw = float(targets.std(dtype=np.float64))
    target_std = 1.0 if target_std_raw < 1e-8 else target_std_raw
    return {
        "inputMean": input_mean,
        "inputStd": input_std,
        "constantFeatures": constant_features,
        "targetMean": target_mean,
        "targetStd": target_std,
        "targetWasConstant": target_std_raw < 1e-8,
    }


def train_network(data: PreparedData, train: np.ndarray, validation: np.ndarray,
                  normalization: dict[str, Any], config: TrainingConfig) -> dict[str, Any]:
    import torch
    from torch import nn

    torch.manual_seed(config.random_seed)
    torch.use_deterministic_algorithms(True)
    network = nn.Sequential(
        nn.Linear(len(FEATURE_NAMES), config.hidden_units),
        nn.ReLU(),
        nn.Linear(config.hidden_units, config.hidden_units),
        nn.ReLU(),
        nn.Linear(config.hidden_units, 1),
    )
    optimizer = torch.optim.Adam(network.parameters(), lr=config.learning_rate)
    loss_fn = nn.MSELoss()
    x_train = torch.from_numpy(normalize_features(data.features[train], normalization))
    y_train = torch.from_numpy(normalize_targets(data.task_returns[train], normalization)).reshape(-1, 1)
    x_validation = torch.from_numpy(normalize_features(data.features[validation], normalization))
    validation_targets = data.task_returns[validation]
    generator = torch.Generator().manual_seed(config.random_seed)
    best_state = copy.deepcopy(network.state_dict())
    best_rmse = math.inf
    best_epoch = 0
    stale = 0
    history = []

    for epoch in range(1, config.max_epochs + 1):
        network.train()
        permutation = torch.randperm(len(x_train), generator=generator)
        sum_loss = 0.0
        for start in range(0, len(permutation), config.batch_size):
            indices = permutation[start:start + config.batch_size]
            optimizer.zero_grad(set_to_none=True)
            loss = loss_fn(network(x_train[indices]), y_train[indices])
            loss.backward()
            optimizer.step()
            sum_loss += float(loss) * len(indices)

        network.eval()
        with torch.no_grad():
            normalized = network(x_validation).reshape(-1).numpy()
        predicted = denormalize_targets(normalized, normalization)
        validation_rmse = float(np.sqrt(np.mean(np.square(predicted - validation_targets), dtype=np.float64)))
        history.append({
            "epoch": epoch,
            "trainMseNormalized": sum_loss / len(x_train),
            "validationRmse": validation_rmse,
        })
        if validation_rmse < best_rmse:
            best_rmse = validation_rmse
            best_epoch = epoch
            best_state = copy.deepcopy(network.state_dict())
            stale = 0
        else:
            stale += 1
            if stale >= config.patience:
                break

    network.load_state_dict(best_state)
    model = build_export_model(network, normalization)
    return {"model": model, "history": history, "bestEpoch": best_epoch, "bestValidationRmse": best_rmse}


def build_export_model(network: Any, normalization: dict[str, Any]) -> Any:
    import torch
    from torch import nn

    class Model(nn.Module):
        def __init__(self) -> None:
            super().__init__()
            self.network = network
            self.register_buffer("input_mean", torch.from_numpy(normalization["inputMean"]))
            self.register_buffer("input_std", torch.from_numpy(normalization["inputStd"]))
            self.register_buffer("target_mean", torch.tensor(normalization["targetMean"], dtype=torch.float32))
            self.register_buffer("target_std", torch.tensor(normalization["targetStd"], dtype=torch.float32))

        def forward(self, combat_state: Any) -> Any:
            normalized = (combat_state - self.input_mean) / self.input_std
            return self.network(normalized) * self.target_std + self.target_mean

    return Model().eval()


def normalize_features(features: np.ndarray, normalization: dict[str, Any]) -> np.ndarray:
    return ((features - normalization["inputMean"]) / normalization["inputStd"]).astype(np.float32)


def normalize_targets(targets: np.ndarray, normalization: dict[str, Any]) -> np.ndarray:
    return ((targets - normalization["targetMean"]) / normalization["targetStd"]).astype(np.float32)


def denormalize_targets(targets: np.ndarray, normalization: dict[str, Any]) -> np.ndarray:
    return targets * normalization["targetStd"] + normalization["targetMean"]


def predict_network(model: Any, features: np.ndarray) -> np.ndarray:
    import torch

    with torch.no_grad():
        return model(torch.from_numpy(features.astype(np.float32))).reshape(-1).numpy()


def fit_baselines(features: np.ndarray, targets: np.ndarray,
                  normalization: dict[str, Any]) -> dict[str, Any]:
    normalized = normalize_features(features, normalization).astype(np.float64)
    design = np.column_stack((np.ones(len(normalized)), normalized))
    coefficients, _, rank, singular = np.linalg.lstsq(design, targets.astype(np.float64), rcond=None)
    normalized_weights = coefficients[1:]
    raw_weights = normalized_weights / normalization["inputStd"]
    raw_intercept = coefficients[0] - np.dot(raw_weights, normalization["inputMean"])
    positive = singular[singular > 0]
    condition = None if len(positive) == 0 else float(positive.max() / positive.min())
    if condition is not None and not math.isfinite(condition):
        condition = None
    return {
        "constant": float(targets.mean(dtype=np.float64)),
        "linearIntercept": float(raw_intercept),
        "linearWeights": raw_weights.astype(np.float64),
        "rank": int(rank),
        "condition": condition,
    }


def predict_linear(features: np.ndarray, baseline: dict[str, Any]) -> np.ndarray:
    return (features.astype(np.float64) @ baseline["linearWeights"] + baseline["linearIntercept"]).astype(np.float32)


def build_metrics(data: PreparedData, heldout: np.ndarray,
                  predictions: dict[str, np.ndarray]) -> dict[str, Any]:
    target = data.task_returns[heldout]
    episode_ids = data.episode_ids[heldout]
    seeds = data.run_seeds[heldout]
    sample_ids = np.asarray([
        f"{data.keys[index].stable_id}/d{int(data.decisions[index])}" for index in heldout
    ])
    predictors = {}
    for name, predicted in predictions.items():
        predictors[name] = metric_block(target, predicted, episode_ids, seeds) | {
            "calibration": calibration(target, predicted, sample_ids)
        }
    return {
        "schema": ARTIFACT_SCHEMA,
        "heldoutTransitions": int(len(heldout)),
        "heldoutEpisodes": int(len(set(episode_ids.tolist()))),
        "heldoutSeeds": sorted(set(int(seed) for seed in seeds)),
        "predictors": predictors,
    }


def metric_block(target: np.ndarray, predicted: np.ndarray, episode_ids: np.ndarray,
                 seeds: np.ndarray) -> dict[str, Any]:
    return {
        "transitionWeighted": errors(target, predicted),
        "episodeMacro": macro_errors(target, predicted, episode_ids),
        "seedMacro": macro_errors(target, predicted, seeds),
    }


def errors(target: np.ndarray, predicted: np.ndarray) -> dict[str, float]:
    residual = predicted.astype(np.float64) - target.astype(np.float64)
    return {
        "mae": float(np.mean(np.abs(residual))),
        "rmse": float(np.sqrt(np.mean(np.square(residual)))),
    }


def macro_errors(target: np.ndarray, predicted: np.ndarray, groups: np.ndarray) -> dict[str, float]:
    blocks = []
    for group in sorted(set(groups.tolist()), key=str):
        mask = groups == group
        blocks.append(errors(target[mask], predicted[mask]))
    return {
        "mae": float(np.mean([block["mae"] for block in blocks])),
        "rmse": float(np.mean([block["rmse"] for block in blocks])),
        "groups": len(blocks),
    }


def calibration(target: np.ndarray, predicted: np.ndarray, sample_ids: np.ndarray,
                bin_count: int = 10) -> dict[str, Any]:
    if float(np.ptp(predicted)) < 1e-12:
        return {
            "status": "undefined_constant_prediction",
            "intercept": None,
            "slope": None,
            "bins": [calibration_bin(target, predicted)],
        }
    design = np.column_stack((np.ones(len(predicted)), predicted.astype(np.float64)))
    coefficients, _, _, _ = np.linalg.lstsq(design, target.astype(np.float64), rcond=None)
    order = np.lexsort((sample_ids, predicted))
    groups = np.array_split(order, min(bin_count, len(order)))
    bins = [calibration_bin(target[index], predicted[index]) for index in groups]
    return {
        "status": "ok",
        "intercept": float(coefficients[0]),
        "slope": float(coefficients[1]),
        "bins": bins,
    }


def calibration_bin(target: np.ndarray, predicted: np.ndarray) -> dict[str, Any]:
    residual = predicted.astype(np.float64) - target.astype(np.float64)
    return {
        "count": int(len(target)),
        "predictionMin": float(np.min(predicted)),
        "predictionMax": float(np.max(predicted)),
        "meanPrediction": float(np.mean(predicted, dtype=np.float64)),
        "meanObserved": float(np.mean(target, dtype=np.float64)),
        "meanResidual": float(np.mean(residual)),
        "mae": float(np.mean(np.abs(residual))),
        "rmse": float(np.sqrt(np.mean(np.square(residual)))),
    }


def comparison(metrics: dict[str, Any]) -> dict[str, Any]:
    neural = metrics["predictors"]["neural"]["transitionWeighted"]
    result = {}
    for name in ("constant", "linear"):
        baseline = metrics["predictors"][name]["transitionWeighted"]
        result[name] = {
            "maeDelta": neural["mae"] - baseline["mae"],
            "rmseDelta": neural["rmse"] - baseline["rmse"],
            "neuralBeatsMae": neural["mae"] < baseline["mae"],
            "neuralBeatsRmse": neural["rmse"] < baseline["rmse"],
        }
    return result


def return_summaries(data: PreparedData) -> dict[str, Any]:
    result = {}
    for split in ("train", "validation", "heldout"):
        index = data.indices(split)
        result[split] = {
            "task": summary(data.task_returns[index]),
            "shapingEnvelope": summary(data.shaping_envelope_returns[index]),
            "shapingBorder": summary(data.shaping_border_returns[index]),
        }
    return result


def summary(values: np.ndarray) -> dict[str, float | int]:
    return {
        "count": int(len(values)),
        "mean": float(np.mean(values, dtype=np.float64)),
        "std": float(np.std(values, dtype=np.float64)),
        "min": float(np.min(values)),
        "max": float(np.max(values)),
    }


def baseline_for_json(baseline: dict[str, Any]) -> dict[str, Any]:
    return {
        "constant": {"value": baseline["constant"]},
        "linear": {
            "intercept": baseline["linearIntercept"],
            "coefficients": [
                {"feature": name, "coefficient": float(value)}
                for name, value in zip(FEATURE_NAMES, baseline["linearWeights"])
            ],
            "rank": baseline["rank"],
            "condition": baseline["condition"],
        },
    }


def write_heldout_predictions(path: Path, data: PreparedData, heldout: np.ndarray,
                              predictions: dict[str, np.ndarray]) -> None:
    rows = []
    for output_index, sample_index in enumerate(heldout):
        target = float(data.task_returns[sample_index])
        row = data.keys[sample_index].as_dict() | {
            "decision": int(data.decisions[sample_index]),
            "source": str(data.sources[sample_index]),
            "combatState": [float(value) for value in data.features[sample_index]],
            "taskReturn": target,
            "shapingEnvelopeReturn": float(data.shaping_envelope_returns[sample_index]),
            "shapingBorderReturn": float(data.shaping_border_returns[sample_index]),
        }
        for name, values in predictions.items():
            value = float(values[output_index])
            row[f"{name}Prediction"] = value
            row[f"{name}Residual"] = value - target
        rows.append(row)
    write_jsonl(path, rows)


def export_onnx(model: Any, path: Path) -> None:
    import onnx
    import torch

    torch.onnx.export(
        model,
        torch.zeros((1, len(FEATURE_NAMES)), dtype=torch.float32),
        path,
        input_names=["combat_state"],
        output_names=["value_return"],
        dynamic_axes={"combat_state": {0: "batch"}, "value_return": {0: "batch"}},
        opset_version=13,
        do_constant_folding=True,
    )
    artifact = onnx.load(path)
    artifact.metadata_props.add(key="artifact_schema", value=ARTIFACT_SCHEMA)
    artifact.metadata_props.add(key="state_schema", value=STATE_SCHEMA)
    artifact.metadata_props.add(key="output_semantics", value="discounted task return; higher is better")
    onnx.checker.check_model(artifact)
    onnx.save(artifact, path)


def verify_onnx(model: Any, path: Path, heldout_features: np.ndarray,
                tolerance: float) -> dict[str, Any]:
    import onnx
    from onnx.reference import ReferenceEvaluator

    artifact = onnx.load(path)
    onnx.checker.check_model(artifact)
    evaluator = ReferenceEvaluator(artifact)
    batches = []
    for size in (1, 128):
        indices = np.arange(size) % len(heldout_features)
        features = heldout_features[indices].astype(np.float32)
        expected = predict_network(model, features).reshape(size, 1)
        actual = evaluator.run(["value_return"], {"combat_state": features})[0]
        maximum = float(np.max(np.abs(expected - actual)))
        if maximum > tolerance:
            raise ValueBaselineError(
                f"ONNX reference inference batch {size} max abs error {maximum} exceeds {tolerance}"
            )
        batches.append({"batchSize": size, "maxAbsError": maximum})
    return {
        "checker": "passed",
        "opset": 13,
        "tolerance": tolerance,
        "referenceInference": batches,
    }


def build_manifest(source_files: Sequence[Path], output_dir: Path, metadata: ArtifactMetadata,
                   config: TrainingConfig, data: PreparedData, normalization: dict[str, Any],
                   trained: dict[str, Any], verification: dict[str, Any]) -> dict[str, Any]:
    import onnx
    import torch

    generated = [
        output_dir / name for name in (
            "value.onnx",
            "metrics.json",
            "baselines.json",
            "training_history.jsonl",
            "episode_audit.jsonl",
            "heldout_predictions.jsonl",
            "verification.json",
        )
    ]
    archived_config = output_dir / "collection_config.yaml"
    if archived_config.exists():
        generated.append(archived_config)
    config_hash = sha256(metadata.collection_config) if metadata.collection_config else None
    return {
        "schema": ARTIFACT_SCHEMA,
        "artifactId": metadata.artifact_id,
        "stateSchema": STATE_SCHEMA,
        "sourceTransitionSchema": TRANSITION_SCHEMA,
        "source": {
            "files": [
                {
                    "path": str(path),
                    "sha256": sha256(path),
                    "bytes": path.stat().st_size,
                    "rows": count_nonempty_lines(path),
                }
                for path in source_files
            ],
            "collectionCommand": metadata.collection_command,
            "collectionConfig": str(metadata.collection_config) if metadata.collection_config else None,
            "collectionConfigSha256": config_hash,
            "collectionConfigArtifact": "collection_config.yaml" if archived_config.exists() else None,
            "collectionMode": "learning_trajectory",
            "sourceCommit": metadata.source_commit or git_head(),
            "knownLimitations": [
                "transition v1 does not carry curriculum stage or opponent identity",
                "transition v1 does not carry gamma; this artifact pins the repository contract at 0.99",
            ],
        },
        "target": {
            "formula": "discounted(dense + timeCost + outcome)",
            "gamma": GAMMA,
            "higherIsBetter": True,
            "terminalBootstrap": 0.0,
            "truncation": "censored_excluded",
            "collectionEnd": "censored_excluded",
            "analysisReturns": ["shapingEnvelope", "shapingBorder"],
        },
        "input": {
            "name": "combat_state",
            "shape": ["batch", len(FEATURE_NAMES)],
            "features": [{"index": index, "name": name} for index, name in enumerate(FEATURE_NAMES)],
            "obstacleTokens": "validated_excluded_v1",
            "actions": "validated_excluded",
        },
        "output": {
            "name": "value_return",
            "shape": ["batch", 1],
            "units": "discounted task return",
            "higherIsBetter": True,
        },
        "normalization": {
            "bakedIntoOnnx": True,
            "fitSplit": "train",
            "inputMean": [float(item) for item in normalization["inputMean"]],
            "inputStd": [float(item) for item in normalization["inputStd"]],
            "constantFeatures": normalization["constantFeatures"],
            "targetMean": normalization["targetMean"],
            "targetStd": normalization["targetStd"],
            "targetWasConstant": normalization["targetWasConstant"],
        },
        "split": {
            "salt": SPLIT_SALT,
            "counts": {
                "train": config.split_counts[0],
                "validation": config.split_counts[1],
                "heldout": config.split_counts[2],
            },
            "seeds": {
                split: sorted(seed for seed, assigned in data.split_by_seed.items() if assigned == split)
                for split in ("train", "validation", "heldout")
            },
            "samples": {
                split: int(len(data.indices(split)))
                for split in ("train", "validation", "heldout")
            },
            "episodesBySeed": episode_counts_by_seed(data.episode_audit),
            "minimumTerminalEpisodesPerSeed": config.min_terminal_episodes_per_seed,
        },
        "training": {
            "device": "cpu",
            "randomSeed": config.random_seed,
            "architecture": [len(FEATURE_NAMES), config.hidden_units, config.hidden_units, 1],
            "activation": "ReLU",
            "loss": "MSE on standardized target",
            "optimizer": "Adam",
            "learningRate": config.learning_rate,
            "batchSize": config.batch_size,
            "maxEpochs": config.max_epochs,
            "patience": config.patience,
            "bestEpoch": trained["bestEpoch"],
            "bestValidationRmse": trained["bestValidationRmse"],
            "selection": "lowest validation RMSE; heldout report only",
        },
        "verification": verification,
        "producer": {
            "entryPoint": "training/rl/train_value_baseline.py",
            "sourceCommit": git_head(),
        },
        "runtime": {
            "python": sys.version.split()[0],
            "platform": platform.platform(),
            "numpy": np.__version__,
            "torch": torch.__version__,
            "onnx": onnx.__version__,
        },
        "files": [
            {"name": path.name, "sha256": sha256(path), "bytes": path.stat().st_size}
            for path in generated
        ],
    }


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def count_nonempty_lines(path: Path) -> int:
    with path.open("r", encoding="utf-8") as stream:
        return sum(1 for line in stream if line.strip())


def episode_counts_by_seed(audit: Sequence[dict[str, Any]]) -> list[dict[str, Any]]:
    counts: dict[tuple[int, str], dict[str, int]] = {}
    for row in audit:
        key = (int(row["runSeed"]), str(row["split"]))
        block = counts.setdefault(key, {"terminal": 0, "truncated": 0, "collection_end": 0})
        block[str(row["endKind"])] += 1
    return [
        {"runSeed": seed, "split": split, **values}
        for (seed, split), values in sorted(counts.items())
    ]


def git_head() -> str | None:
    result = subprocess.run(
        ["git", "rev-parse", "HEAD"], capture_output=True, text=True, check=False
    )
    return result.stdout.strip() if result.returncode == 0 else None


def write_json(path: Path, value: Any) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True, allow_nan=False) + "\n", encoding="utf-8")


def write_jsonl(path: Path, values: Iterable[dict[str, Any]]) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        for value in values:
            stream.write(json.dumps(value, sort_keys=True, allow_nan=False) + "\n")
