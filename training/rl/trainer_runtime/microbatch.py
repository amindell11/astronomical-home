from dataclasses import dataclass
from typing import Dict, Iterable, List, Tuple

import numpy as np
from mlagents.trainers.action_info import ActionInfo
from mlagents.trainers.behavior_id_utils import get_global_agent_id
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.torch_entities.action_log_probs import LogProbsTuple
from mlagents_envs.base_env import ActionTuple, BehaviorName, DecisionSteps


MICROBATCH_WINDOW_MICROS = 500


@dataclass(frozen=True)
class MicrobatchSettings:
    requested_worker_cap: int
    effective_worker_cap: int
    window_micros: int = MICROBATCH_WINDOW_MICROS

    @staticmethod
    def create(requested_worker_cap: int, num_envs: int) -> "MicrobatchSettings":
        return MicrobatchSettings(
            requested_worker_cap=requested_worker_cap,
            effective_worker_cap=min(requested_worker_cap, num_envs),
        )


@dataclass
class MicrobatchStats:
    policy_forward_count: int = 0
    worker_request_count: int = 0
    agent_row_count: int = 0
    max_workers_per_forward: int = 0

    @property
    def mean_workers_per_forward(self) -> float:
        if self.policy_forward_count == 0:
            return 0.0
        return self.worker_request_count / self.policy_forward_count


@dataclass(frozen=True)
class _Request:
    worker_id: int
    behavior_name: BehaviorName
    decision_steps: DecisionSteps


@dataclass
class _PolicyGroup:
    policy: TorchPolicy
    requests: List[_Request]


class InferenceMicrobatch:
    def __init__(self, settings: MicrobatchSettings):
        self.settings = settings
        self.stats = MicrobatchStats()

    def register_policy(self, policy) -> None:
        if self.settings.effective_worker_cap > 1 and not isinstance(policy, TorchPolicy):
            raise TypeError(
                "microbatch worker cap "
                f"{self.settings.requested_worker_cap} requires TorchPolicy; "
                f"registered {type(policy).__module__}.{type(policy).__qualname__}"
            )

    def actions_for_workers(
        self,
        worker_steps: Iterable[Tuple[int, Dict]],
        policies: Dict[BehaviorName, object],
    ) -> Dict[int, Dict[BehaviorName, ActionInfo]]:
        steps = list(worker_steps)
        actions = {worker_id: {} for worker_id, _ in steps}
        if self.settings.effective_worker_cap == 1:
            self._sequential_actions(steps, policies, actions)
            return actions

        groups: List[_PolicyGroup] = []
        for worker_id, all_step_result in steps:
            for behavior_name, (decision_steps, _terminal_steps) in all_step_result.items():
                policy = policies.get(behavior_name)
                if policy is None:
                    continue
                self.register_policy(policy)
                if len(decision_steps) == 0:
                    actions[worker_id][behavior_name] = ActionInfo.empty()
                    continue
                group = next(
                    (candidate for candidate in groups if candidate.policy is policy),
                    None,
                )
                if group is None:
                    group = _PolicyGroup(policy, [])
                    groups.append(group)
                group.requests.append(_Request(worker_id, behavior_name, decision_steps))

        for group in groups:
            for requests in self._worker_chunks(group.requests):
                partitioned = self._evaluate(group.policy, requests)
                for request, action_info in zip(requests, partitioned):
                    actions[request.worker_id][request.behavior_name] = action_info
        return actions

    def _sequential_actions(self, steps, policies, actions) -> None:
        for worker_id, all_step_result in steps:
            for behavior_name, (decision_steps, _terminal_steps) in all_step_result.items():
                policy = policies.get(behavior_name)
                if policy is None:
                    continue
                actions[worker_id][behavior_name] = policy.get_action(
                    decision_steps, worker_id
                )
                if len(decision_steps) > 0:
                    self._record_forward(1, len(decision_steps))

    def _worker_chunks(self, requests: List[_Request]) -> Iterable[List[_Request]]:
        chunk: List[_Request] = []
        workers = set()
        for request in requests:
            if (
                request.worker_id not in workers
                and len(workers) == self.settings.effective_worker_cap
            ):
                yield chunk
                chunk = []
                workers = set()
            chunk.append(request)
            workers.add(request.worker_id)
        if chunk:
            yield chunk

    def _evaluate(self, policy: TorchPolicy, requests: List[_Request]) -> List[ActionInfo]:
        merged = _merge_decision_steps(policy, requests)
        global_agent_ids = [
            get_global_agent_id(request.worker_id, int(agent_id))
            for request in requests
            for agent_id in request.decision_steps.agent_id
        ]
        outputs = policy.evaluate(merged, global_agent_ids)
        partitioned_outputs = _split_outputs(
            outputs, [len(request.decision_steps) for request in requests]
        )
        policy.save_memories(global_agent_ids, outputs.get("memory_out"))
        policy.check_nan_action(outputs.get("action"))
        self._record_forward(len({request.worker_id for request in requests}), len(merged))
        return [
            ActionInfo(
                action=part["action"],
                env_action=part["env_action"],
                outputs=part,
                agent_ids=list(request.decision_steps.agent_id),
            )
            for request, part in zip(requests, partitioned_outputs)
        ]

    def _record_forward(self, worker_count: int, agent_rows: int) -> None:
        self.stats.policy_forward_count += 1
        self.stats.worker_request_count += worker_count
        self.stats.agent_row_count += agent_rows
        self.stats.max_workers_per_forward = max(
            self.stats.max_workers_per_forward, worker_count
        )


def _merge_decision_steps(policy: TorchPolicy, requests: List[_Request]) -> DecisionSteps:
    decisions = [request.decision_steps for request in requests]
    masks = None
    if any(decision.action_mask is not None for decision in decisions):
        branches = policy.behavior_spec.action_spec.discrete_branches
        masks = [
            np.concatenate([
                np.zeros((len(decision), branch_size), dtype=bool)
                if decision.action_mask is None
                else decision.action_mask[index]
                for decision in decisions
            ])
            for index, branch_size in enumerate(branches)
        ]
    return DecisionSteps(
        obs=[
            np.concatenate([decision.obs[index] for decision in decisions])
            for index in range(len(decisions[0].obs))
        ],
        reward=np.concatenate([decision.reward for decision in decisions]),
        agent_id=np.concatenate([decision.agent_id for decision in decisions]),
        action_mask=masks,
        group_id=np.concatenate([decision.group_id for decision in decisions]),
        group_reward=np.concatenate([decision.group_reward for decision in decisions]),
    )


def _split_outputs(outputs: Dict, row_counts: List[int]) -> List[Dict]:
    required = {"action", "env_action", "log_probs", "entropy"}
    allowed = required | {"memory_out"}
    unknown = set(outputs) - allowed
    missing = required - set(outputs)
    if unknown or missing:
        raise RuntimeError(
            f"unsupported TorchPolicy batched output keys: unknown={sorted(unknown)}, "
            f"missing={sorted(missing)}"
        )
    partitions = [dict() for _ in row_counts]
    offset = 0
    for row_count, partition in zip(row_counts, partitions):
        end = offset + row_count
        for key, value in outputs.items():
            partition[key] = _slice_output(key, value, offset, end)
        offset = end
    return partitions


def _slice_output(key: str, value, start: int, end: int):
    if key in ("action", "env_action") and isinstance(value, ActionTuple):
        return _slice_action_tuple(value, start, end)
    if key == "log_probs" and isinstance(value, LogProbsTuple):
        return _slice_action_tuple(value, start, end)
    if key in ("entropy", "memory_out") and isinstance(value, np.ndarray):
        return value[start:end]
    raise TypeError(
        f"unsupported TorchPolicy batched output {key!r} type "
        f"{type(value).__module__}.{type(value).__qualname__}"
    )


def _slice_action_tuple(value, start: int, end: int):
    continuous = None if value.continuous is None else value.continuous[start:end]
    discrete = None if value.discrete is None else value.discrete[start:end]
    return type(value)(continuous=continuous, discrete=discrete)
