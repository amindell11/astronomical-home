using System;
using UnityEngine;
using RL.Opponents;
using RL.Reward;

namespace RL.Episodes
{
    public enum EpisodeOutcome { Unresolved, Win, Loss, Draw }

    /// <summary>How an episode ended: Terminal = game-semantic end (death/out-of-bounds), Truncation = harness cutoff (timeout).</summary>
    public enum EndKind { None, Terminal, Truncation }

    [Serializable]
    public struct EpisodeResult
    {
        public string schema;
        public int episodeIndex;
        public string outcome;
        public string endKind;
        public bool mutualKill;
        public bool agentExitedBounds;
        public bool baselineExitedBounds;
        public bool timedOut;
        public int decisions;
        public float simSeconds;

        public float startMyPool;
        public float startEnemyPool;
        public float endMyPool;
        public float endEnemyPool;

        public RewardSpec spec;
        public OpponentDraw opponent;

        public const string SchemaId = "rl-episode-v8";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }
}
