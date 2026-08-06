import hashlib
import json
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Optional

MANIFEST_NAME = "run_manifest.json"
SUMMARIES_NAME = "summaries.jsonl"


def config_sha256(path: Path) -> str:
    """The run's config identity — one definition so every producer/consumer hashes alike."""
    return hashlib.sha256(path.read_bytes()).hexdigest()


@dataclass(frozen=True)
class RunManifest:
    run_id: str
    behavior: str
    results_dir: Path
    started_at: datetime
    max_steps: int
    mode: str
    config_hash: str

    @property
    def run_dir(self) -> Path:
        return self.results_dir / self.run_id


@dataclass(frozen=True)
class TrainingSummary:
    behavior: str
    step: int
    elapsed_seconds: float
    max_steps: int
    mean_reward: Optional[float]
    reward_std_dev: Optional[float]
    elo: Optional[float]
    is_training: bool


def manifest_path(results_dir: Path, run_id: str) -> Path:
    return results_dir / run_id / MANIFEST_NAME


def summaries_path(run_dir: Path) -> Path:
    return run_dir / SUMMARIES_NAME


def write_manifest(path: Path, manifest: RunManifest) -> None:
    data = {
        "runId": manifest.run_id,
        "behavior": manifest.behavior,
        "resultsDir": str(manifest.results_dir),
        "startedAt": manifest.started_at.isoformat().replace("+00:00", "Z"),
        "maxSteps": manifest.max_steps,
        "mode": manifest.mode,
        "configHash": manifest.config_hash,
    }
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def read_manifest(path: Path) -> RunManifest:
    data = json.loads(path.read_text(encoding="utf-8"))
    started_at = datetime.fromisoformat(data["startedAt"].replace("Z", "+00:00"))
    return RunManifest(
        run_id=str(data["runId"]),
        behavior=str(data["behavior"]),
        results_dir=Path(data["resultsDir"]),
        started_at=started_at,
        max_steps=int(data["maxSteps"]),
        mode=str(data["mode"]),
        config_hash=str(data["configHash"]),
    )


def read_summaries(path: Path) -> list[TrainingSummary]:
    if not path.exists():
        return []
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()
    if text and not text.endswith("\n"):
        lines = lines[:-1]
    return [_parse_summary(json.loads(line)) for line in lines if line.strip()]


def _parse_summary(data: dict) -> TrainingSummary:
    return TrainingSummary(
        behavior=str(data["behavior"]),
        step=int(data["step"]),
        elapsed_seconds=float(data["elapsedSeconds"]),
        max_steps=int(data["maxSteps"]),
        mean_reward=_optional_float(data.get("meanReward")),
        reward_std_dev=_optional_float(data.get("rewardStdDev")),
        elo=_optional_float(data.get("elo")),
        is_training=bool(data["isTraining"]),
    )


def _optional_float(value) -> Optional[float]:
    return None if value is None else float(value)
