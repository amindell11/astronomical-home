import unittest
from unittest.mock import Mock

import numpy as np
from mlagents.torch_utils import torch
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.settings import NetworkSettings
from mlagents.trainers.torch_entities.action_log_probs import LogProbsTuple
from mlagents.trainers.torch_entities.agent_action import AgentAction
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents_envs.base_env import (
    ActionSpec,
    ActionTuple,
    BehaviorSpec,
    DecisionSteps,
    DimensionProperty,
    ObservationSpec,
    ObservationType,
)

from trainer_runtime.microbatch import (
    InferenceMicrobatch,
    MicrobatchSettings,
    _Request,
    _merge_decision_steps,
)


def production_spec():
    return BehaviorSpec(
        observation_specs=[
            ObservationSpec(
                (64, 7),
                (DimensionProperty.VARIABLE_SIZE, DimensionProperty.NONE),
                ObservationType.DEFAULT,
                "AsteroidBuffer",
            ),
            ObservationSpec(
                (28,),
                (DimensionProperty.NONE,),
                ObservationType.DEFAULT,
                "VectorSensor_size28",
            ),
        ],
        action_spec=ActionSpec(5, (2, 2)),
    )


def production_policy(seed=7, recurrent=False):
    memory = (
        NetworkSettings.MemorySettings(sequence_length=8, memory_size=16)
        if recurrent
        else None
    )
    return TorchPolicy(
        seed,
        production_spec(),
        NetworkSettings(hidden_units=32, num_layers=1, memory=memory),
        SimpleActor,
        {"conditional_sigma": False, "tanh_squash": False},
    )


def decisions(agent_ids, offset=0.0, action_mask=None):
    count = len(agent_ids)
    rng = np.random.default_rng(100 + int(offset))
    buffer_obs = rng.normal(size=(count, 64, 7)).astype(np.float32)
    vector_obs = rng.normal(size=(count, 28)).astype(np.float32) + offset
    return DecisionSteps(
        obs=[buffer_obs, vector_obs],
        reward=np.arange(count, dtype=np.float32) + offset,
        agent_id=np.asarray(agent_ids, dtype=np.int32),
        action_mask=action_mask,
        group_id=np.arange(count, dtype=np.int32) + 20,
        group_reward=np.arange(count, dtype=np.float32) + offset + 0.5,
    )


def empty_decisions():
    return DecisionSteps.empty(production_spec())


def output_rows(count):
    continuous = np.arange(count * 5, dtype=np.float32).reshape(count, 5)
    discrete = np.arange(count * 2, dtype=np.int32).reshape(count, 2) % 2
    return {
        "action": ActionTuple(continuous=continuous, discrete=discrete),
        "env_action": ActionTuple(continuous=continuous / 3, discrete=discrete),
        "log_probs": LogProbsTuple(
            continuous=continuous / 10, discrete=continuous[:, :2] / 10
        ),
        "entropy": np.arange(count, dtype=np.float32),
        "memory_out": np.arange(count * 4, dtype=np.float32).reshape(count, 4),
    }


class MicrobatchPartitionTests(unittest.TestCase):
    def test_groups_by_exact_policy_identity_in_first_appearance_order(self):
        first_policy = production_policy(seed=1)
        second_policy = production_policy(seed=2)
        calls = []

        def evaluator(name):
            def evaluate(merged, global_ids):
                calls.append((name, list(global_ids)))
                return output_rows(len(merged))

            return evaluate

        for name, policy in (("first", first_policy), ("second", second_policy)):
            policy.evaluate = Mock(side_effect=evaluator(name))
            policy.save_memories = Mock()
            policy.check_nan_action = Mock()
        microbatch = InferenceMicrobatch(MicrobatchSettings.create(2, 2))

        microbatch.actions_for_workers(
            [
                (0, {
                    "SecondBehavior": (decisions([10]), None),
                    "FirstBehavior": (decisions([11], offset=1), None),
                }),
                (1, {
                    "SecondBehavior": (decisions([12], offset=2), None),
                    "FirstBehavior": (decisions([13], offset=3), None),
                }),
            ],
            {"FirstBehavior": first_policy, "SecondBehavior": second_policy},
        )

        self.assertEqual(["second", "first"], [name for name, _ in calls])
        self.assertEqual(
            ["agent_0-10", "agent_1-12"], calls[0][1]
        )
        self.assertEqual(
            ["agent_0-11", "agent_1-13"], calls[1][1]
        )

    def test_worker_cap_partitions_one_policy_into_multiple_forwards(self):
        policy = production_policy()
        policy.evaluate = Mock(side_effect=lambda merged, _ids: output_rows(len(merged)))
        policy.save_memories = Mock()
        policy.check_nan_action = Mock()
        microbatch = InferenceMicrobatch(MicrobatchSettings.create(2, 3))

        microbatch.actions_for_workers(
            [
                (worker_id, {"ShipCombat": (decisions([worker_id + 1]), None)})
                for worker_id in range(3)
            ],
            {"ShipCombat": policy},
        )

        self.assertEqual(2, policy.evaluate.call_count)
        self.assertEqual(2, microbatch.stats.policy_forward_count)
        self.assertEqual(3, microbatch.stats.worker_request_count)
        self.assertEqual(2, microbatch.stats.max_workers_per_forward)
        self.assertEqual(1.5, microbatch.stats.mean_workers_per_forward)

    def test_merges_all_fields_masks_and_splits_known_outputs(self):
        policy = production_policy()
        captured = {}

        def evaluate(merged, global_ids):
            captured["merged"] = merged
            captured["global_ids"] = global_ids
            return output_rows(len(merged))

        policy.evaluate = Mock(side_effect=evaluate)
        policy.save_memories = Mock()
        policy.check_nan_action = Mock()
        first = decisions([7], offset=1)
        second = decisions(
            [8, 9],
            offset=2,
            action_mask=[
                np.array([[True, False], [False, True]]),
                np.array([[False, False], [True, False]]),
            ],
        )
        microbatch = InferenceMicrobatch(MicrobatchSettings.create(2, 2))

        actions = microbatch.actions_for_workers(
            [(0, {"ShipCombat": (first, None)}), (1, {"ShipCombat": (second, None)})],
            {"ShipCombat": policy},
        )

        merged = captured["merged"]
        self.assertEqual([7, 8, 9], list(merged.agent_id))
        np.testing.assert_array_equal([1, 2, 3], merged.reward)
        np.testing.assert_array_equal([20, 20, 21], merged.group_id)
        np.testing.assert_array_equal([1.5, 2.5, 3.5], merged.group_reward)
        np.testing.assert_array_equal([[False, False]], merged.action_mask[0][:1])
        np.testing.assert_array_equal(second.action_mask[0], merged.action_mask[0][1:])
        self.assertEqual(
            ["agent_0-7", "agent_1-8", "agent_1-9"], captured["global_ids"]
        )
        self.assertEqual([7], actions[0]["ShipCombat"].agent_ids)
        self.assertEqual([8, 9], actions[1]["ShipCombat"].agent_ids)
        np.testing.assert_array_equal(
            output_rows(3)["action"].continuous[:1],
            actions[0]["ShipCombat"].action.continuous,
        )
        np.testing.assert_array_equal(
            output_rows(3)["memory_out"][1:],
            actions[1]["ShipCombat"].outputs["memory_out"],
        )
        policy.save_memories.assert_called_once()
        policy.check_nan_action.assert_called_once()
        self.assertEqual(1, microbatch.stats.policy_forward_count)
        self.assertEqual(2, microbatch.stats.worker_request_count)
        self.assertEqual(3, microbatch.stats.agent_row_count)
        self.assertEqual(2.0, microbatch.stats.mean_workers_per_forward)

    def test_empty_decisions_remain_empty_without_a_forward(self):
        policy = production_policy()
        policy.evaluate = Mock()
        microbatch = InferenceMicrobatch(MicrobatchSettings.create(2, 2))

        actions = microbatch.actions_for_workers(
            [(0, {"ShipCombat": (empty_decisions(), None)})], {"ShipCombat": policy}
        )

        self.assertEqual([], actions[0]["ShipCombat"].agent_ids)
        policy.evaluate.assert_not_called()
        self.assertEqual(0, microbatch.stats.policy_forward_count)

    def test_cap_one_dispatches_the_unmodified_get_action_path_for_any_policy(self):
        expected = object()
        policy = Mock()
        policy.get_action.return_value = expected
        decision_steps = decisions([4])
        microbatch = InferenceMicrobatch(MicrobatchSettings.create(1, 6))

        actions = microbatch.actions_for_workers(
            [(3, {"ShipCombat": (decision_steps, None)})], {"ShipCombat": policy}
        )

        self.assertIs(expected, actions[3]["ShipCombat"])
        policy.get_action.assert_called_once_with(decision_steps, 3)

    def test_batched_registration_refuses_non_torch_policy(self):
        microbatch = InferenceMicrobatch(MicrobatchSettings.create(6, 6))
        with self.assertRaisesRegex(TypeError, "requires TorchPolicy.*Mock"):
            microbatch.register_policy(Mock())

    def test_unknown_output_key_and_type_fail_loudly(self):
        policy = production_policy()
        microbatch = InferenceMicrobatch(MicrobatchSettings.create(2, 2))
        policy.evaluate = Mock(return_value={**output_rows(1), "future_value": np.zeros(1)})
        with self.assertRaisesRegex(RuntimeError, "future_value"):
            microbatch.actions_for_workers(
                [(0, {"ShipCombat": (decisions([1]), None)})], {"ShipCombat": policy}
            )

        outputs = output_rows(1)
        outputs["entropy"] = [0.0]
        policy.evaluate = Mock(return_value=outputs)
        with self.assertRaisesRegex(TypeError, "entropy.*builtins.list"):
            microbatch.actions_for_workers(
                [(0, {"ShipCombat": (decisions([1]), None)})], {"ShipCombat": policy}
            )


class ProductionPolicyEquivalenceTests(unittest.TestCase):
    def test_real_recurrent_policy_partitions_memory_by_worker(self):
        policy = production_policy(seed=13, recurrent=True)
        microbatch = InferenceMicrobatch(MicrobatchSettings.create(2, 2))

        actions = microbatch.actions_for_workers(
            [
                (0, {"ShipCombat": (decisions([1]), None)}),
                (1, {"ShipCombat": (decisions([2, 3], offset=2), None)}),
            ],
            {"ShipCombat": policy},
        )

        self.assertEqual((1, 16), actions[0]["ShipCombat"].outputs["memory_out"].shape)
        self.assertEqual((2, 16), actions[1]["ShipCombat"].outputs["memory_out"].shape)
        self.assertEqual(3, len(policy.memory_dict))

    def test_batched_distribution_fixed_action_log_prob_and_entropy_match_sequential(self):
        torch.manual_seed(11)
        policy = production_policy(seed=11)
        first = decisions([1], offset=1)
        second = decisions(
            [2, 3],
            offset=2,
            action_mask=[
                np.array([[False, True], [False, False]]),
                np.array([[False, False], [True, False]]),
            ],
        )
        requests = [_Request(0, "ShipCombat", first), _Request(1, "ShipCombat", second)]
        merged = _merge_decision_steps(policy, requests)

        sequential = [self._distribution_read(policy, item) for item in (first, second)]
        batched = self._distribution_read(policy, merged)
        for index, (expected, count) in enumerate(zip(sequential, (1, 2))):
            start = 0 if index == 0 else 1
            end = start + count
            for expected_array, batched_array in zip(expected, batched):
                np.testing.assert_allclose(
                    expected_array,
                    batched_array[start:end],
                    rtol=1e-6,
                    atol=1e-6,
                )

    @staticmethod
    def _distribution_read(policy, decision_steps):
        tensor_obs = [torch.as_tensor(obs) for obs in decision_steps.obs]
        masks = policy._extract_masks(decision_steps)
        with torch.no_grad():
            encoding, _ = policy.actor.network_body(tensor_obs, memories=None, sequence_length=1)
            distributions = policy.actor.action_model._get_dists(encoding, masks)
            fixed = AgentAction(
                torch.zeros((len(decision_steps), 5)),
                [torch.zeros((len(decision_steps), 1), dtype=torch.long) for _ in range(2)],
            )
            log_probs, entropy = policy.actor.action_model._get_probs_and_entropy(
                fixed, distributions
            )
        arrays = [
            distributions.continuous.mean.numpy(),
            distributions.continuous.std.numpy(),
            *[distribution.logits.numpy() for distribution in distributions.discrete],
            log_probs.continuous_tensor.numpy(),
            log_probs.discrete_tensor.squeeze(1).numpy(),
            entropy.numpy(),
        ]
        return arrays


if __name__ == "__main__":
    unittest.main()
