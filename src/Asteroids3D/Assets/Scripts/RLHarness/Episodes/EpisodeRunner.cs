using System.Collections.Generic;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Host-agnostic 1v1 episode loop: the driver calls <see cref="Tick"/> once per fixed step; the runner owns the decision-boundary clock (every K steps → snapshot, pay reward), termination, and result assembly. Plain class so a training scene and the PlayMode tests host the same object.</summary>
    public class EpisodeRunner
    {
        private readonly Ship agent;
        private readonly Ship baseline;
        private readonly RewardSpec spec;
        private readonly Vector2 arenaCenter;
        private readonly bool tracePerDecision;

        private CombatSnapshot prev;
        private float phiEnvelopePrev;
        private float phiBorderPrev;
        private int stepsSinceDecision;
        private int totalSteps;
        private bool begun;
        private EpisodeResult result;
        private BoundaryResult lastBoundary;

        public bool IsDone { get; private set; }
        public EpisodeResult Result => result;
        /// <summary>The reward decomposition of the most recently paid boundary; on the terminal boundary it carries the outcome reward and end kind.</summary>
        public BoundaryResult LastBoundary => lastBoundary;
        /// <summary>The snapshot captured at the most recent boundary (Begin pose, then each paid decision, including the terminal one) — the observation moment for a decision-synchronized sensor.</summary>
        public CombatSnapshot BoundarySnapshot { get; private set; }
        /// <summary>The snapshot captured this fixed step (Begin pose before the first Tick) — what a per-step observer reads instead of re-deriving the same capture.</summary>
        public CombatSnapshot StepSnapshot { get; private set; }

        public EpisodeRunner(Ship agent, Ship baseline, RewardSpec spec, int episodeIndex,
            Vector2 arenaCenter, bool tracePerDecision = false)
        {
            this.agent = agent;
            this.baseline = baseline;
            this.spec = spec;
            this.arenaCenter = arenaCenter;
            this.tracePerDecision = tracePerDecision;
            result = new EpisodeResult
            {
                schema = EpisodeResult.SchemaId,
                observationSize = AgentObservations.CombatChannels,
                obstacleTokenCap = AgentObservations.ObstacleTokenCap,
                obstacleTokenFloats = AgentObservations.ObstacleTokenFloats,
                episodeIndex = episodeIndex,
                outcome = EpisodeOutcome.Unresolved.ToString(),
                endKind = EndKind.None.ToString(),
                spec = spec,
                trace = tracePerDecision ? new List<DecisionRow>() : null,
            };
        }

        public void RecordOpponent(in OpponentDraw draw) => result.opponent = draw;

        /// <summary>Re-baselines the reward snapshot at the episode's start pose; call after the pair-reset, before the first Tick.</summary>
        public void Begin()
        {
            prev = CombatSnapshotExtractor.Capture(agent, baseline, arenaCenter);
            BoundarySnapshot = prev;
            StepSnapshot = prev;
            phiEnvelopePrev = PotentialShaping.EnvelopePhi(in prev, in spec);
            phiBorderPrev = PotentialShaping.BorderPhi(in prev, in spec);
            result.startMyPool = prev.myPool;
            result.startEnemyPool = prev.enemyPool;
            result.startPhiEnvelope = phiEnvelopePrev;
            result.startPhiBorder = phiBorderPrev;
            begun = true;
        }

        /// <summary>Advance one fixed step; returns true when a decision boundary was paid this tick (<see cref="LastBoundary"/> holds its result).</summary>
        public bool Tick()
        {
            if (!begun || IsDone) return false;
            totalSteps++;
            stepsSinceDecision++;

            var next = CombatSnapshotExtractor.Capture(agent, baseline, arenaCenter);
            StepSnapshot = next;
            var verdict = EpisodeRules.Evaluate(in next, in spec);
            var boundary = stepsSinceDecision >= spec.decisionIntervalSteps;

            var endKind = verdict.outcome != EpisodeOutcome.Unresolved ? EndKind.Terminal : EndKind.None;
            if (endKind == EndKind.None && boundary && result.decisions + 1 >= spec.timeoutDecisions)
            {
                endKind = EndKind.Truncation;
                verdict.outcome = EpisodeOutcome.Draw;
                result.timedOut = true;
            }

            if (endKind == EndKind.None && !boundary) return false;

            PayDecision(in next, endKind);
            stepsSinceDecision = 0;
            BoundarySnapshot = next;

            if (endKind != EndKind.None) Finish(in verdict, in next, endKind);
            else prev = next;
            return true;
        }

        private void PayDecision(in CombatSnapshot next, EndKind endKind)
        {
            result.decisions++;

            var dense = RewardTerms.PoolDifferential(in prev, in next, spec.lambda);
            var phiEnvelopeNext = PotentialShaping.EnvelopePhi(in next, in spec);
            var phiBorderNext = PotentialShaping.BorderPhi(in next, in spec);
            var terminal = endKind == EndKind.Terminal;
            var shapingEnvelope = PotentialShaping.Step(phiEnvelopePrev, phiEnvelopeNext, terminal);
            var shapingBorder = PotentialShaping.Step(phiBorderPrev, phiBorderNext, terminal);
            var timeCost = -spec.timeCostPerDecision;

            result.sumDense += dense;
            result.sumShapingEnvelope += shapingEnvelope;
            result.sumShapingBorder += shapingBorder;
            result.sumTimeCost += timeCost;
            switch (endKind)
            {
                case EndKind.None:
                    result.midPhiEnvelopeSum += phiEnvelopeNext;
                    result.midPhiBorderSum += phiBorderNext;
                    break;
                case EndKind.Truncation:
                    result.endPhiEnvelope = phiEnvelopeNext;
                    result.endPhiBorder = phiBorderNext;
                    break;
            }

            result.trace?.Add(new DecisionRow
            {
                decision = result.decisions,
                dense = dense,
                shapingEnvelope = shapingEnvelope,
                shapingBorder = shapingBorder,
                phiEnvelope = terminal ? 0f : phiEnvelopeNext,
                phiBorder = terminal ? 0f : phiBorderNext,
            });

            lastBoundary = new BoundaryResult
            {
                decision = result.decisions,
                dense = dense,
                shapingEnvelope = shapingEnvelope,
                shapingBorder = shapingBorder,
                timeCost = timeCost,
                endKind = endKind,
            };

            phiEnvelopePrev = phiEnvelopeNext;
            phiBorderPrev = phiBorderNext;
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
            result.outcomeReward = RewardTerms.Outcome(verdict.outcome);
            result.totalReward = result.sumDense + result.sumShapingEnvelope
                + result.sumShapingBorder + result.sumTimeCost + result.outcomeReward;
            lastBoundary.outcomeReward = result.outcomeReward;
            IsDone = true;
        }
    }
}
