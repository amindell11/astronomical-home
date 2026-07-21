using System;
using System.Collections.Generic;
using Combat.Weapons;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One degeneracy-gate episode's opponent-centric signals; outcome fields are agent-centric (Win = the archetype died). Per-archetype signals share one row shape — fields another archetype does not exercise stay zero.</summary>
    [Serializable]
    public struct ArchetypeGateRow
    {
        public string schema;
        public int seed;
        public int episodeIndex;
        public OpponentDraw opponent;
        public string outcome;
        public string endKind;
        public bool opponentSurvived;
        public bool opponentExitedBounds;
        public float simSeconds;
        public float startRange;
        public float endRange;
        public float meanRange;
        public float borderHugFraction;
        public float rangeBandFraction;
        public int shotsFired;
        public int agentShotsFired;
        public float opponentPoolLostPct;
        public float agentPoolLostPct;
        public float meanOrbitRadiusError;
        public float maxDisplacement;

        public const string SchemaId = "rl-archetype-gate-v2";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>Per-archetype aggregate over a gate sweep — the human go/no-go numbers.</summary>
    [Serializable]
    public struct ArchetypeGateSummary
    {
        public string schema;
        public string archetype;
        public int episodes;
        public int survived;
        public int died;
        public int exitedBounds;
        public float meanSimSeconds;
        public float meanStartRange;
        public float meanEndRange;
        public float meanRange;
        public float meanBorderHugFraction;
        public float meanRangeBandFraction;
        public float meanShotsFired;
        public float meanAgentShotsFired;
        public float meanOpponentPoolLostPct;
        public float meanAgentPoolLostPct;
        public float meanOrbitRadiusError;
        public float maxDisplacement;

        public const string SchemaId = "rl-archetype-gate-summary-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);

        public static ArchetypeGateSummary Summarize(string archetype, IReadOnlyList<ArchetypeGateRow> rows)
        {
            var summary = new ArchetypeGateSummary
            {
                schema = SchemaId,
                archetype = archetype,
                episodes = rows.Count,
            };
            foreach (var row in rows)
            {
                if (row.opponentSurvived) summary.survived++;
                else summary.died++;
                if (row.opponentExitedBounds) summary.exitedBounds++;
                summary.meanSimSeconds += row.simSeconds;
                summary.meanStartRange += row.startRange;
                summary.meanEndRange += row.endRange;
                summary.meanRange += row.meanRange;
                summary.meanBorderHugFraction += row.borderHugFraction;
                summary.meanRangeBandFraction += row.rangeBandFraction;
                summary.meanShotsFired += row.shotsFired;
                summary.meanAgentShotsFired += row.agentShotsFired;
                summary.meanOpponentPoolLostPct += row.opponentPoolLostPct;
                summary.meanAgentPoolLostPct += row.agentPoolLostPct;
                summary.meanOrbitRadiusError += row.meanOrbitRadiusError;
                summary.maxDisplacement = Mathf.Max(summary.maxDisplacement, row.maxDisplacement);
            }
            if (rows.Count > 0)
            {
                var n = rows.Count;
                summary.meanSimSeconds /= n;
                summary.meanStartRange /= n;
                summary.meanEndRange /= n;
                summary.meanRange /= n;
                summary.meanBorderHugFraction /= n;
                summary.meanRangeBandFraction /= n;
                summary.meanShotsFired /= n;
                summary.meanAgentShotsFired /= n;
                summary.meanOpponentPoolLostPct /= n;
                summary.meanAgentPoolLostPct /= n;
                summary.meanOrbitRadiusError /= n;
            }
            return summary;
        }
    }

    /// <summary>Per-fixed-step opponent-behavior sampler for one gate episode: range statistics, border-hug and range-band occupancy, both sides' shots (via <see cref="WeaponComponent.OnFire"/>), and displacement from spawn. Construct after the pair-reset (it baselines on the spawn pose); dispose to unhook the fire counters.</summary>
    public sealed class ArchetypeGateProbe : IDisposable
    {
        /// <summary>Kiter band half-width around its drawn hold range.</summary>
        private const float RangeBand = 4f;
        /// <summary>Rim occupancy threshold — outside this fraction of the arena radius counts as border-hugging.</summary>
        private const float HugRadiusFraction = 0.85f;

        private readonly Ship opponent;
        private readonly Ship agent;
        private readonly Vector2 arenaCenter;
        private readonly float borderHugRadius;
        private readonly OpponentDraw draw;
        private readonly WeaponComponent[] opponentWeapons;
        private readonly WeaponComponent[] agentWeapons;
        private readonly Vector2 spawnPos;

        private int steps;
        private float rangeSum;
        private float startRange;
        private float endRange;
        private int borderHugSteps;
        private int rangeBandSteps;
        private float orbitErrorSum;
        private float maxDisplacementSq;
        private int shotsFired;
        private int agentShotsFired;

        public ArchetypeGateProbe(Ship opponent, Ship agent, Vector2 arenaCenter, float arenaRadius,
            in OpponentDraw draw)
        {
            this.opponent = opponent;
            this.agent = agent;
            this.arenaCenter = arenaCenter;
            this.draw = draw;
            borderHugRadius = arenaRadius * HugRadiusFraction;
            opponentWeapons = opponent.GetComponentsInChildren<WeaponComponent>();
            foreach (var weapon in opponentWeapons)
                weapon.OnFire += CountShot;
            agentWeapons = agent.GetComponentsInChildren<WeaponComponent>();
            foreach (var weapon in agentWeapons)
                weapon.OnFire += CountAgentShot;
            spawnPos = opponent.Kinematics.pos;
            startRange = Range();
            endRange = startRange;
        }

        public void Sample()
        {
            if (!opponent || !agent) return;
            var pos = opponent.Kinematics.pos;
            var range = Range();
            steps++;
            rangeSum += range;
            endRange = range;
            if ((pos - arenaCenter).magnitude > borderHugRadius) borderHugSteps++;
            if (Mathf.Abs(range - draw.desiredRange) <= RangeBand) rangeBandSteps++;
            if (draw.orbitRadius > 0f) orbitErrorSum += Mathf.Abs(range - draw.orbitRadius);
            maxDisplacementSq = Mathf.Max(maxDisplacementSq, (pos - spawnPos).sqrMagnitude);
        }

        public ArchetypeGateRow ToRow(in EpisodeResult result) => new()
        {
            schema = ArchetypeGateRow.SchemaId,
            seed = result.spec.runSeed,
            episodeIndex = result.episodeIndex,
            opponent = draw,
            outcome = result.outcome,
            endKind = result.endKind,
            opponentSurvived = result.outcome != EpisodeOutcome.Win.ToString() && !result.mutualKill,
            opponentExitedBounds = result.baselineExitedBounds,
            simSeconds = result.simSeconds,
            startRange = startRange,
            endRange = endRange,
            meanRange = steps > 0 ? rangeSum / steps : startRange,
            borderHugFraction = steps > 0 ? (float)borderHugSteps / steps : 0f,
            rangeBandFraction = steps > 0 ? (float)rangeBandSteps / steps : 0f,
            shotsFired = shotsFired,
            agentShotsFired = agentShotsFired,
            opponentPoolLostPct = result.startEnemyPool > 0f
                ? (result.startEnemyPool - result.endEnemyPool) / result.startEnemyPool : 0f,
            agentPoolLostPct = result.startMyPool > 0f
                ? (result.startMyPool - result.endMyPool) / result.startMyPool : 0f,
            meanOrbitRadiusError = steps > 0 ? orbitErrorSum / steps : 0f,
            maxDisplacement = Mathf.Sqrt(maxDisplacementSq),
        };

        public void Dispose()
        {
            foreach (var weapon in opponentWeapons)
                if (weapon) weapon.OnFire -= CountShot;
            foreach (var weapon in agentWeapons)
                if (weapon) weapon.OnFire -= CountAgentShot;
        }

        private void CountShot() => shotsFired++;
        private void CountAgentShot() => agentShotsFired++;

        private float Range() => (opponent.Kinematics.pos - agent.Kinematics.pos).magnitude;
    }
}
