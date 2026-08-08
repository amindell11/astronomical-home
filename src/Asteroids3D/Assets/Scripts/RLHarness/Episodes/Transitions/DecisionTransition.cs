using System;
using UnityEngine;

namespace Game.RLHarness
{
    [Serializable]
    public struct DecisionObservation
    {
        public float[] combat;
        public float[] obstacleTokens;

        public static DecisionObservation Copy(float[] combat, float[] obstacleTokens, int obstacleTokenCount)
        {
            var combatCopy = new float[AgentObservations.CombatChannels];
            Array.Copy(combat, combatCopy, combatCopy.Length);

            var obstacleFloats = obstacleTokenCount * AgentObservations.ObstacleTokenFloats;
            var obstacleCopy = new float[obstacleFloats];
            Array.Copy(obstacleTokens, obstacleCopy, obstacleFloats);
            return new DecisionObservation { combat = combatCopy, obstacleTokens = obstacleCopy };
        }
    }

    [Serializable]
    public struct ExecutedDecisionAction
    {
        public float[] continuous;
        public int[] discrete;
        public bool boostExecuted;
    }

    [Serializable]
    public struct DecisionReward
    {
        public float dense;
        public float shapingEnvelope;
        public float shapingBorder;
        public float timeCost;
        public float outcome;
        public float total;

        public static DecisionReward From(in BoundaryResult boundary) => new()
        {
            dense = boundary.dense,
            shapingEnvelope = boundary.shapingEnvelope,
            shapingBorder = boundary.shapingBorder,
            timeCost = boundary.timeCost,
            outcome = boundary.outcomeReward,
            total = boundary.Total,
        };
    }

    [Serializable]
    public struct DecisionTransition
    {
        public string schema;
        public int observationSize;
        public int obstacleTokenCap;
        public int obstacleTokenFloats;
        public int continuousActionSize;
        public int[] discreteActionBranches;
        public string[] rewardFields;

        public string runId;
        public int workerIndex;
        public int arenaIndex;
        public int runSeed;
        public int episodeIndex;
        public int teamId;
        public int decision;

        public DecisionObservation state;
        public ExecutedDecisionAction action;
        public DecisionReward reward;
        public DecisionObservation nextState;
        public bool terminal;
        public bool truncated;

        public const string SchemaId = "rl-transition-v1";

        public static readonly string[] RewardFieldNames =
        {
            nameof(DecisionReward.dense),
            nameof(DecisionReward.shapingEnvelope),
            nameof(DecisionReward.shapingBorder),
            nameof(DecisionReward.timeCost),
            nameof(DecisionReward.outcome),
        };

        public static DecisionTransition Create(string runId, int workerIndex, int arenaIndex,
            in RewardSpec spec, int episodeIndex, int teamId, in DecisionObservation state,
            in ExecutedDecisionAction action, in BoundaryResult boundary,
            in DecisionObservation nextState) => new()
        {
            schema = SchemaId,
            observationSize = AgentObservations.CombatChannels,
            obstacleTokenCap = AgentObservations.ObstacleTokenCap,
            obstacleTokenFloats = AgentObservations.ObstacleTokenFloats,
            continuousActionSize = AgentActions.Count,
            discreteActionBranches = new[]
            {
                AgentActions.ChoicesPerBranch,
                AgentActions.ChoicesPerBranch,
            },
            rewardFields = RewardFieldNames,
            runId = runId,
            workerIndex = workerIndex,
            arenaIndex = arenaIndex,
            runSeed = spec.runSeed,
            episodeIndex = episodeIndex,
            teamId = teamId,
            decision = boundary.decision,
            state = state,
            action = action,
            reward = DecisionReward.From(in boundary),
            nextState = nextState,
            terminal = boundary.endKind == EndKind.Terminal,
            truncated = boundary.endKind == EndKind.Truncation,
        };

        public void Validate()
        {
            Require(schema == SchemaId, $"transition schema '{schema}' must be {SchemaId}");
            Require(observationSize == AgentObservations.CombatChannels,
                $"observationSize {observationSize} must be {AgentObservations.CombatChannels}");
            Require(obstacleTokenCap == AgentObservations.ObstacleTokenCap,
                $"obstacleTokenCap {obstacleTokenCap} must be {AgentObservations.ObstacleTokenCap}");
            Require(obstacleTokenFloats == AgentObservations.ObstacleTokenFloats,
                $"obstacleTokenFloats {obstacleTokenFloats} must be {AgentObservations.ObstacleTokenFloats}");
            Require(continuousActionSize == AgentActions.Count,
                $"continuousActionSize {continuousActionSize} must be {AgentActions.Count}");
            Require(discreteActionBranches is { Length: 2 }
                    && discreteActionBranches[0] == AgentActions.ChoicesPerBranch
                    && discreteActionBranches[1] == AgentActions.ChoicesPerBranch,
                "discreteActionBranches must declare two binary branches");
            Require(rewardFields is { Length: 5 }, "rewardFields must declare all five components");
            for (var i = 0; i < RewardFieldNames.Length; i++)
                Require(rewardFields[i] == RewardFieldNames[i],
                    $"rewardFields[{i}] '{rewardFields[i]}' must be '{RewardFieldNames[i]}'");

            Require(!string.IsNullOrWhiteSpace(runId), "runId must identify the producing run");
            Require(workerIndex >= 0, $"workerIndex {workerIndex} must be non-negative");
            Require(arenaIndex >= 0, $"arenaIndex {arenaIndex} must be non-negative");
            Require(episodeIndex >= 0, $"episodeIndex {episodeIndex} must be non-negative");
            Require(teamId is 0 or 1, $"teamId {teamId} must be 0 or 1");
            Require(decision > 0, $"decision {decision} must be positive");
            ValidateObservation(in state, nameof(state));
            ValidateObservation(in nextState, nameof(nextState));
            Require(action.continuous is { Length: AgentActions.Count },
                $"action.continuous must contain {AgentActions.Count} values");
            Require(action.discrete is { Length: 2 }, "action.discrete must contain two branches");
            for (var i = 0; i < action.discrete.Length; i++)
                Require(action.discrete[i] is 0 or 1,
                    $"action.discrete[{i}] {action.discrete[i]} must be 0 or 1");
            Require(!(terminal && truncated), "a transition cannot be both terminal and truncated");

            var rewardSum = reward.dense + reward.shapingEnvelope + reward.shapingBorder
                + reward.timeCost + reward.outcome;
            Require(Mathf.Approximately(reward.total, rewardSum),
                $"reward.total {reward.total} must equal its components {rewardSum}");
        }

        public string ToJsonLine() => JsonUtility.ToJson(this);

        private static void ValidateObservation(in DecisionObservation observation, string name)
        {
            Require(observation.combat is { Length: AgentObservations.CombatChannels },
                $"{name}.combat must contain {AgentObservations.CombatChannels} values");
            Require(observation.obstacleTokens != null,
                $"{name}.obstacleTokens must be an empty or populated array");
            Require(observation.obstacleTokens.Length % AgentObservations.ObstacleTokenFloats == 0,
                $"{name}.obstacleTokens must contain whole obstacle tokens");
            Require(observation.obstacleTokens.Length
                    <= AgentObservations.ObstacleTokenCap * AgentObservations.ObstacleTokenFloats,
                $"{name}.obstacleTokens exceeds the declared cap");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
