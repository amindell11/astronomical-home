using System.Collections.Generic;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>
    /// Host-agnostic 1v1 episode loop: the driver calls <see cref="Tick"/> once per fixed step;
    /// the runner owns the decision-boundary clock (every K steps → snapshot, pay reward),
    /// termination, and result assembly. Plain class so PR-3's training scene can host the
    /// same object the PlayMode tests drive.
    /// </summary>
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

        public bool IsDone { get; private set; }
        public EpisodeResult Result => result;

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
                episodeIndex = episodeIndex,
                outcome = EpisodeOutcome.Unresolved.ToString(),
                spec = spec,
                trace = tracePerDecision ? new List<DecisionRow>() : null,
            };
        }

        /// <summary>Re-baselines the reward snapshot at the episode's start pose; call after the pair-reset, before the first Tick.</summary>
        public void Begin()
        {
            prev = CombatSnapshotExtractor.Capture(agent, baseline, arenaCenter);
            phiEnvelopePrev = PotentialShaping.EnvelopePhi(in prev, in spec);
            phiBorderPrev = PotentialShaping.BorderPhi(in prev, in spec);
            result.startMyPool = prev.myPool;
            result.startEnemyPool = prev.enemyPool;
            result.startPhiEnvelope = phiEnvelopePrev;
            result.startPhiBorder = phiBorderPrev;
            begun = true;
        }

        public void Tick()
        {
            if (!begun || IsDone) return;
            totalSteps++;
            stepsSinceDecision++;

            var next = CombatSnapshotExtractor.Capture(agent, baseline, arenaCenter);
            var verdict = EpisodeRules.Evaluate(in next, in spec);
            var boundary = stepsSinceDecision >= spec.decisionIntervalSteps;

            if (verdict.outcome == EpisodeOutcome.Unresolved
                && boundary && result.decisions + 1 >= spec.timeoutDecisions)
            {
                verdict.outcome = EpisodeOutcome.Draw;
                result.timedOut = true;
            }

            var terminal = verdict.outcome != EpisodeOutcome.Unresolved;
            if (!terminal && !boundary) return;

            PayDecision(in next, terminal);
            stepsSinceDecision = 0;

            if (terminal) Finish(verdict, in next);
            else prev = next;
        }

        private void PayDecision(in CombatSnapshot next, bool terminal)
        {
            result.decisions++;

            var dense = RewardTerms.PoolDifferential(in prev, in next, spec.lambda);
            var phiEnvelopeNext = PotentialShaping.EnvelopePhi(in next, in spec);
            var phiBorderNext = PotentialShaping.BorderPhi(in next, in spec);
            var shapingEnvelope = PotentialShaping.Step(phiEnvelopePrev, phiEnvelopeNext, spec.gamma, terminal);
            var shapingBorder = PotentialShaping.Step(phiBorderPrev, phiBorderNext, spec.gamma, terminal);

            result.sumDense += dense;
            result.sumShapingEnvelope += shapingEnvelope;
            result.sumShapingBorder += shapingBorder;
            if (!terminal)
            {
                result.midPhiEnvelopeSum += phiEnvelopeNext;
                result.midPhiBorderSum += phiBorderNext;
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

            phiEnvelopePrev = phiEnvelopeNext;
            phiBorderPrev = phiBorderNext;
        }

        private void Finish(in EpisodeRules.Verdict verdict, in CombatSnapshot last)
        {
            result.outcome = verdict.outcome.ToString();
            result.mutualKill = verdict.mutualKill;
            result.agentExitedBounds = verdict.agentExitedBounds;
            result.baselineExitedBounds = verdict.baselineExitedBounds;
            result.simSeconds = totalSteps * Time.fixedDeltaTime;
            result.endMyPool = last.myPool;
            result.endEnemyPool = last.enemyPool;
            result.outcomeReward = RewardTerms.Outcome(verdict.outcome);
            result.totalReward = result.sumDense + result.sumShapingEnvelope
                + result.sumShapingBorder + result.outcomeReward;
            IsDone = true;
        }
    }
}
