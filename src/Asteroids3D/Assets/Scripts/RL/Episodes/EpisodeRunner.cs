using Ships;
using UnityEngine;
using RL.Opponents;
using RL.Reward;

namespace RL.Episodes
{
    /// <summary>Host-agnostic 1v1 episode loop: the driver calls <see cref="Tick"/> once per fixed step; the runner owns the decision-boundary clock (every K steps counts one decision toward the timeout), termination, and result assembly. A win waits until every tracked extra opponent is dead too. Plain class so the harness host and the PlayMode tests host the same object.</summary>
    public class EpisodeRunner
    {
        private readonly Ship agent;
        private readonly Ship baseline;
        private readonly RewardSpec spec;
        private readonly Vector2 arenaCenter;

        private Ship[] extraOpponents = System.Array.Empty<Ship>();
        private int stepsSinceDecision;
        private int totalSteps;
        private bool begun;
        private EpisodeResult result;

        public bool IsDone { get; private set; }
        public EpisodeResult Result => result;

        public EpisodeRunner(Ship agent, Ship baseline, RewardSpec spec, int episodeIndex, Vector2 arenaCenter)
        {
            this.agent = agent;
            this.baseline = baseline;
            this.spec = spec;
            this.arenaCenter = arenaCenter;
            result = new EpisodeResult
            {
                schema = EpisodeResult.SchemaId,
                episodeIndex = episodeIndex,
                outcome = EpisodeOutcome.Unresolved.ToString(),
                endKind = EndKind.None.ToString(),
                spec = spec,
            };
        }

        public void RecordOpponent(in OpponentDraw draw) => result.opponent = draw;

        public void TrackExtraOpponents(Ship[] opponents) =>
            extraOpponents = opponents ?? throw new System.ArgumentNullException(nameof(opponents));

        /// <summary>Records the episode's start pools; call after the pair-reset, before the first Tick.</summary>
        public void Begin()
        {
            var start = CombatSnapshotExtractor.Capture(agent, baseline, arenaCenter);
            result.startMyPool = start.myPool;
            result.startEnemyPool = start.enemyPool;
            begun = true;
        }

        /// <summary>Advance one fixed step: count a decision at each boundary, and end the episode on a terminal verdict or at the timeout.</summary>
        public void Tick()
        {
            if (!begun || IsDone) return;
            totalSteps++;
            stepsSinceDecision++;

            var next = CombatSnapshotExtractor.Capture(agent, baseline, arenaCenter);
            var verdict = EpisodeRules.Evaluate(in next, in spec);
            if (verdict.outcome == EpisodeOutcome.Win && ExtraLiving())
                verdict.outcome = EpisodeOutcome.Unresolved;
            var boundary = stepsSinceDecision >= spec.decisionIntervalSteps;

            var endKind = verdict.outcome != EpisodeOutcome.Unresolved ? EndKind.Terminal : EndKind.None;
            if (endKind == EndKind.None && boundary && result.decisions + 1 >= spec.timeoutDecisions)
            {
                endKind = EndKind.Truncation;
                verdict.outcome = EpisodeOutcome.Draw;
                result.timedOut = true;
            }

            if (endKind == EndKind.None && !boundary) return;

            result.decisions++;
            stepsSinceDecision = 0;

            if (endKind != EndKind.None) Finish(in verdict, in next, endKind);
        }

        private bool ExtraLiving()
        {
            for (var i = 0; i < extraOpponents.Length; i++)
            {
                var ship = extraOpponents[i];
                if (ship && ship.Damage.Health.CurrentValue > 0f) return true;
            }
            return false;
        }

        private void Finish(in EpisodeRules.Verdict verdict, in CombatSnapshot last, EndKind endKind)
        {
            result.outcome = verdict.outcome.ToString();
            result.endKind = endKind.ToString();
            result.mutualKill = verdict.mutualKill;
            result.agentExitedBounds = verdict.agentExitedBounds;
            result.baselineExitedBounds = verdict.baselineExitedBounds;
            result.simSeconds = totalSteps * Time.fixedDeltaTime;
            result.endMyPool = last.myPool;
            result.endEnemyPool = last.enemyPool;
            IsDone = true;
        }
    }
}
