import json
import os
import shutil
from collections import deque
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable, Deque, Optional

from mlagents.torch_utils import torch
from mlagents.trainers.model_saver.torch_model_saver import (
    DEFAULT_CHECKPOINT_NAME,
    TorchModelSaver,
)
from mlagents.trainers.settings import SerializationSettings, TrainerSettings
from mlagents.trainers.training_status import GlobalTrainingStatus, StatusMetaData, StatusType

from trainer_runtime.contract import checkpoint_manifest_path, repair_torn_jsonl


@dataclass(frozen=True)
class PendingCheckpoint:
    step: int
    onnx_path: Path
    pt_path: Path
    pointer_tmp_path: Path
    pointer_path: Path
    completed_at: datetime


def atomic_write_training_status(path: Path) -> None:
    GlobalTrainingStatus.saved_state[
        StatusType.STATS_METADATA.value
    ] = StatusMetaData().to_dict()
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    with temporary.open("w", encoding="utf-8") as handle:
        json.dump(GlobalTrainingStatus.saved_state, handle, indent=4)
        handle.flush()
        os.fsync(handle.fileno())
    os.replace(temporary, path)


class CheckpointManifestWriter:
    def __init__(self, run_dir: Path, resume: bool):
        self.run_dir = run_dir
        self.path = checkpoint_manifest_path(run_dir)
        if resume:
            repair_torn_jsonl(self.path)
        else:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            self.path.write_text("", encoding="utf-8")

    def append(self, checkpoint: PendingCheckpoint) -> None:
        data = {
            "step": checkpoint.step,
            "onnx": checkpoint.onnx_path.relative_to(self.run_dir).as_posix(),
            "pt": checkpoint.pt_path.relative_to(self.run_dir).as_posix(),
            "completedAt": checkpoint.completed_at.isoformat().replace("+00:00", "Z"),
        }
        with self.path.open("a", encoding="utf-8") as handle:
            handle.write(json.dumps(data, separators=(",", ":")) + "\n")
            handle.flush()


class AtomicTorchModelSaver(TorchModelSaver):
    def __init__(
        self,
        trainer_settings: TrainerSettings,
        model_path: str,
        load: bool = False,
        stage_hook: Optional[Callable[[str], None]] = None,
    ):
        super().__init__(trainer_settings, model_path, load)
        self.pending: Deque[PendingCheckpoint] = deque()
        self.pending_final_models: Deque[Path] = deque()
        self._stage_hook = stage_hook or (lambda _stage: None)

    def save_checkpoint(self, behavior_name: str, step: int):
        model_dir = Path(self.model_path)
        model_dir.mkdir(parents=True, exist_ok=True)
        stem = model_dir / f"{behavior_name}-{step}"
        pt_path = stem.with_suffix(".pt")
        onnx_path = stem.with_suffix(".onnx")
        state_dict = {name: module.state_dict() for name, module in self.modules.items()}

        pt_tmp = pt_path.with_name(pt_path.name + ".tmp")
        torch.save(state_dict, pt_tmp)
        _fsync_file(pt_tmp)
        os.replace(pt_tmp, pt_path)
        self._stage_hook("interval_pt")

        onnx_tmp_stem = Path(str(stem) + ".tmp")
        self.export(str(onnx_tmp_stem), behavior_name)
        onnx_tmp = Path(str(onnx_tmp_stem) + ".onnx")
        _fsync_file(onnx_tmp)
        os.replace(onnx_tmp, onnx_path)
        self._stage_hook("interval_onnx")

        pointer_path = model_dir / DEFAULT_CHECKPOINT_NAME
        pointer_tmp = pointer_path.with_name(pointer_path.name + ".tmp")
        torch.save(state_dict, pointer_tmp)
        _fsync_file(pointer_tmp)
        self._stage_hook("pointer_staged")

        self.pending.append(PendingCheckpoint(
            step=step,
            onnx_path=onnx_path,
            pt_path=pt_path,
            pointer_tmp_path=pointer_tmp,
            pointer_path=pointer_path,
            completed_at=datetime.now(timezone.utc),
        ))
        return str(onnx_path), [str(pt_path)]

    def copy_final_model(self, source_nn_path: str) -> None:
        if SerializationSettings.convert_to_onnx:
            self.pending_final_models.append(Path(source_nn_path).with_suffix(".onnx"))

    def publish_final_models(self) -> None:
        destination = Path(self.model_path).with_suffix(".onnx")
        while self.pending_final_models:
            source = self.pending_final_models.popleft()
            temporary = destination.with_name(destination.name + ".tmp")
            try:
                shutil.copyfile(source, temporary)
                _fsync_file(temporary)
                os.replace(temporary, destination)
            except OSError:
                if temporary.exists():
                    temporary.unlink()


class CheckpointCommitter:
    def __init__(
        self,
        run_dir: Path,
        status_path: Path,
        resume: bool,
        stage_hook: Optional[Callable[[str], None]] = None,
    ):
        self.manifest = CheckpointManifestWriter(run_dir, resume)
        self.status_path = status_path
        self._stage_hook = stage_hook or (lambda _stage: None)

    def commit(self, saver: AtomicTorchModelSaver, trainer) -> None:
        while saver.pending:
            checkpoint = saver.pending[0]
            self._mirror_elo(trainer)
            self.manifest.append(checkpoint)
            self._stage_hook("manifest")
            atomic_write_training_status(self.status_path)
            self._stage_hook("status")
            os.replace(checkpoint.pointer_tmp_path, checkpoint.pointer_path)
            self._stage_hook("pointer")
            saver.pending.popleft()

    @staticmethod
    def _mirror_elo(trainer) -> None:
        if not hasattr(trainer, "current_elo"):
            return
        GlobalTrainingStatus.set_parameter_state(
            trainer.brain_name, StatusType.ELO, trainer.current_elo
        )


def _fsync_file(path: Path) -> None:
    with path.open("r+b") as handle:
        os.fsync(handle.fileno())
