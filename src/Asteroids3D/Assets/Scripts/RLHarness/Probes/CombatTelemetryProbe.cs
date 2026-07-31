using System;
using System.Collections.Generic;
using System.IO;
using Combat.Conditions;
using Combat.Weapons;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One engagement's entry record plus what happened inside it and in the gap before it.</summary>
    [Serializable]
    public struct CombatEngagementRow
    {
        public float entrySimSeconds;
        public float agentShieldPct;
        public float agentHeatPct;
        public float agentPoolPct;
        public float oppShieldPct;
        public float oppHeatPct;
        public float oppPoolPct;
        public int agentBoostsGapBefore;
        public int oppBoostsGapBefore;
        public int agentBoosts;
        public int oppBoosts;
    }

    /// <summary>One episode's combat-telemetry signals: range-band occupancy normalized to the agent's primary FireRange, TTK inputs, engagement cycles, regen events split engaged/disengaged, and boost activations per engagement and per gap.</summary>
    [Serializable]
    public struct CombatTelemetryRow
    {
        public string schema;
        public int episodeIndex;
        public OpponentDraw opponent;
        public string opponentLabel;
        public string outcome;
        public string endKind;
        public float simSeconds;
        public float[] rangeOccupancy;
        public float meanRange;
        public float minRange;
        public float maxRange;
        public float fireDistance;
        public int agentShots;
        public int oppShots;
        public List<CombatEngagementRow> engagements;
        public int agentRegenEventsEngaged;
        public float agentRegenAmountEngaged;
        public int agentRegenEventsDisengaged;
        public float agentRegenAmountDisengaged;
        public int oppRegenEventsEngaged;
        public float oppRegenAmountEngaged;
        public int oppRegenEventsDisengaged;
        public float oppRegenAmountDisengaged;
        public int agentBoostsTrailingGap;
        public int oppBoostsTrailingGap;

        public const string SchemaId = "rl-combat-telemetry-v1";
        /// <summary>Quarter-envelope bins to 2.0× the normalizer, plus one overflow bin.</summary>
        public const int RangeBands = 9;

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>Per-opponent means/counts over a sweep — distribution math stays reader-side.</summary>
    [Serializable]
    public struct CombatTelemetrySummary
    {
        public string schema;
        public string opponent;
        public int episodes;
        public float meanSimSeconds;
        public float[] meanRangeOccupancy;
        public float meanRange;
        public float meanMinRange;
        public float meanMaxRange;
        public float meanFireDistance;
        public float meanAgentShots;
        public float meanOppShots;
        public float meanEngagements;
        public float meanAgentRegenEventsEngaged;
        public float meanAgentRegenAmountEngaged;
        public float meanAgentRegenEventsDisengaged;
        public float meanAgentRegenAmountDisengaged;
        public float meanOppRegenEventsEngaged;
        public float meanOppRegenAmountEngaged;
        public float meanOppRegenEventsDisengaged;
        public float meanOppRegenAmountDisengaged;
        public float meanAgentBoostsEngaged;
        public float meanAgentBoostsGap;
        public float meanOppBoostsEngaged;
        public float meanOppBoostsGap;

        public const string SchemaId = "rl-combat-telemetry-summary-v1";

        public static CombatTelemetrySummary Summarize(string opponent, IReadOnlyList<CombatTelemetryRow> rows)
        {
            var summary = new CombatTelemetrySummary
            {
                schema = SchemaId,
                opponent = opponent,
                episodes = rows.Count,
                meanRangeOccupancy = new float[CombatTelemetryRow.RangeBands],
            };
            foreach (var row in rows)
            {
                summary.meanSimSeconds += row.simSeconds;
                for (var i = 0; i < summary.meanRangeOccupancy.Length; i++)
                    summary.meanRangeOccupancy[i] += row.rangeOccupancy[i];
                summary.meanRange += row.meanRange;
                summary.meanMinRange += row.minRange;
                summary.meanMaxRange += row.maxRange;
                summary.meanFireDistance += row.fireDistance;
                summary.meanAgentShots += row.agentShots;
                summary.meanOppShots += row.oppShots;
                summary.meanEngagements += row.engagements.Count;
                summary.meanAgentRegenEventsEngaged += row.agentRegenEventsEngaged;
                summary.meanAgentRegenAmountEngaged += row.agentRegenAmountEngaged;
                summary.meanAgentRegenEventsDisengaged += row.agentRegenEventsDisengaged;
                summary.meanAgentRegenAmountDisengaged += row.agentRegenAmountDisengaged;
                summary.meanOppRegenEventsEngaged += row.oppRegenEventsEngaged;
                summary.meanOppRegenAmountEngaged += row.oppRegenAmountEngaged;
                summary.meanOppRegenEventsDisengaged += row.oppRegenEventsDisengaged;
                summary.meanOppRegenAmountDisengaged += row.oppRegenAmountDisengaged;
                foreach (var engagement in row.engagements)
                {
                    summary.meanAgentBoostsEngaged += engagement.agentBoosts;
                    summary.meanOppBoostsEngaged += engagement.oppBoosts;
                    summary.meanAgentBoostsGap += engagement.agentBoostsGapBefore;
                    summary.meanOppBoostsGap += engagement.oppBoostsGapBefore;
                }
                summary.meanAgentBoostsGap += row.agentBoostsTrailingGap;
                summary.meanOppBoostsGap += row.oppBoostsTrailingGap;
            }
            if (rows.Count > 0)
            {
                var n = rows.Count;
                summary.meanSimSeconds /= n;
                for (var i = 0; i < summary.meanRangeOccupancy.Length; i++)
                    summary.meanRangeOccupancy[i] /= n;
                summary.meanRange /= n;
                summary.meanMinRange /= n;
                summary.meanMaxRange /= n;
                summary.meanFireDistance /= n;
                summary.meanAgentShots /= n;
                summary.meanOppShots /= n;
                summary.meanEngagements /= n;
                summary.meanAgentRegenEventsEngaged /= n;
                summary.meanAgentRegenAmountEngaged /= n;
                summary.meanAgentRegenEventsDisengaged /= n;
                summary.meanAgentRegenAmountDisengaged /= n;
                summary.meanOppRegenEventsEngaged /= n;
                summary.meanOppRegenAmountEngaged /= n;
                summary.meanOppRegenEventsDisengaged /= n;
                summary.meanOppRegenAmountDisengaged /= n;
                summary.meanAgentBoostsEngaged /= n;
                summary.meanAgentBoostsGap /= n;
                summary.meanOppBoostsEngaged /= n;
                summary.meanOppBoostsGap /= n;
            }
            return summary;
        }
    }

    /// <summary>The engagement state machine: engaged while either ship's firing envelope is valid, exit only after a full hysteresis window with neither.</summary>
    public sealed class EngagementTracker
    {
        // τ mirrors EnemyTracker.combatExitDelay: momentary envelope/LOS dropouts must not split an engagement.
        public const float ExitHysteresisSeconds = 3f;

        public enum Transition { None, Enter, Exit }

        private readonly float exitSeconds;
        private float lastValidTime;

        public bool Engaged { get; private set; }

        public EngagementTracker(float exitSeconds = ExitHysteresisSeconds) => this.exitSeconds = exitSeconds;

        public Transition Step(float time, bool envelopeValid)
        {
            if (envelopeValid)
            {
                lastValidTime = time;
                if (Engaged) return Transition.None;
                Engaged = true;
                return Transition.Enter;
            }
            if (!Engaged || time - lastValidTime < exitSeconds) return Transition.None;
            Engaged = false;
            return Transition.Exit;
        }

        /// <summary>Episode end: an open engagement closes and counts.</summary>
        public bool Close()
        {
            var wasEngaged = Engaged;
            Engaged = false;
            return wasEngaged;
        }
    }

    // Only boost use drops BoostAvailable, so the falling edge is an activation.
    internal sealed class BoostEdgeDetector
    {
        private bool available;

        public BoostEdgeDetector(bool available) => this.available = available;

        public bool Step(bool nowAvailable)
        {
            var activated = available && !nowAvailable;
            available = nowAvailable;
            return activated;
        }
    }

    /// <summary>Regen events off Shield.OnValueChanged: a rising tick is regen (damage only falls), routed by engagement state.</summary>
    internal sealed class RegenSplit
    {
        public int EngagedEvents { get; private set; }
        public float EngagedAmount { get; private set; }
        public int DisengagedEvents { get; private set; }
        public float DisengagedAmount { get; private set; }

        public void Observe(float value, float previous, bool engaged)
        {
            if (value <= previous) return;
            var amount = value - previous;
            if (engaged)
            {
                EngagedEvents++;
                EngagedAmount += amount;
            }
            else
            {
                DisengagedEvents++;
                DisengagedAmount += amount;
            }
        }
    }

    /// <summary>Per-fixed-step combat sampler for one episode — a pure observer over <see cref="CombatSnapshotExtractor.Capture"/>, both sides' <see cref="WeaponComponent.OnFire"/>, Shield.OnValueChanged, and polled boost availability. Construct after the pair-reset (the reset's shield refill must not read as regen); dispose to unhook.</summary>
    public sealed class CombatTelemetrySampler : IDisposable
    {
        private readonly Ship agent;
        private readonly Ship baseline;
        private readonly Vector2 arenaCenter;
        private readonly OpponentDraw draw;
        private readonly float fireDistance;
        private readonly IHeatReadout agentHeat;
        private readonly IHeatReadout oppHeat;
        private readonly WeaponComponent[] agentWeapons;
        private readonly WeaponComponent[] oppWeapons;
        private readonly BoostEdgeDetector agentBoostEdge;
        private readonly BoostEdgeDetector oppBoostEdge;
        private readonly EngagementTracker tracker = new();
        private readonly RegenSplit agentRegen = new();
        private readonly RegenSplit oppRegen = new();
        private readonly List<CombatEngagementRow> engagements = new();
        private readonly int[] bandSteps = new int[CombatTelemetryRow.RangeBands];

        private CombatEngagementRow open;
        private int steps;
        private float rangeSum;
        private float minRange = float.MaxValue;
        private float maxRange;
        private int agentShots;
        private int oppShots;
        private int agentBoostsGap;
        private int oppBoostsGap;

        public CombatTelemetrySampler(EpisodePair pair, Vector2 arenaCenter, in OpponentDraw draw)
        {
            agent = pair.Agent;
            baseline = pair.Baseline;
            this.arenaCenter = arenaCenter;
            this.draw = draw;
            var primary = agent.Weapons ? agent.Weapons.Primary : null;
            fireDistance = primary ? primary.FireRange : 0f;
            if (fireDistance <= 0f)
                throw new InvalidOperationException(
                    $"{CombatTelemetryProbe.ProbeName} probe: agent primary FireRange must be > 0 — it is the range-band normalizer.");
            agentHeat = ResolvePrimaryHeat(agent);
            oppHeat = ResolvePrimaryHeat(baseline);
            agentWeapons = agent.GetComponentsInChildren<WeaponComponent>();
            foreach (var weapon in agentWeapons)
                weapon.OnFire += CountAgentShot;
            oppWeapons = baseline.GetComponentsInChildren<WeaponComponent>();
            foreach (var weapon in oppWeapons)
                weapon.OnFire += CountOppShot;
            agent.Damage.Shield.OnValueChanged += OnAgentShieldChanged;
            baseline.Damage.Shield.OnValueChanged += OnOppShieldChanged;
            agentBoostEdge = new BoostEdgeDetector(agent.BoostAvailable);
            oppBoostEdge = new BoostEdgeDetector(baseline.BoostAvailable);
        }

        internal static int BandIndex(float normalizedRange) =>
            normalizedRange < 2f ? (int)(normalizedRange * 4f) : CombatTelemetryRow.RangeBands - 1;

        public void Sample()
        {
            if (!agent || !baseline) return;
            steps++;
            var snapshot = CombatSnapshotExtractor.Capture(agent, baseline, arenaCenter);
            var range = snapshot.distanceToTarget;
            rangeSum += range;
            if (range < minRange) minRange = range;
            if (range > maxRange) maxRange = range;
            bandSteps[BandIndex(range / fireDistance)]++;

            var time = steps * Time.fixedDeltaTime;
            switch (tracker.Step(time, snapshot.inMyEnvelope || snapshot.inEnemyEnvelope))
            {
                case EngagementTracker.Transition.Enter:
                    OpenEngagement(time, in snapshot);
                    break;
                case EngagementTracker.Transition.Exit:
                    engagements.Add(open);
                    break;
            }
            if (agentBoostEdge.Step(agent.BoostAvailable)) CountBoost(ref open.agentBoosts, ref agentBoostsGap);
            if (oppBoostEdge.Step(baseline.BoostAvailable)) CountBoost(ref open.oppBoosts, ref oppBoostsGap);
        }

        public CombatTelemetryRow ToRow(in EpisodeResult result, string opponentLabel)
        {
            if (tracker.Close()) engagements.Add(open);
            var occupancy = new float[CombatTelemetryRow.RangeBands];
            if (steps > 0)
                for (var i = 0; i < occupancy.Length; i++)
                    occupancy[i] = (float)bandSteps[i] / steps;
            return new CombatTelemetryRow
            {
                schema = CombatTelemetryRow.SchemaId,
                episodeIndex = result.episodeIndex,
                opponent = draw,
                opponentLabel = opponentLabel,
                outcome = result.outcome,
                endKind = result.endKind,
                simSeconds = result.simSeconds,
                rangeOccupancy = occupancy,
                meanRange = steps > 0 ? rangeSum / steps : 0f,
                minRange = minRange == float.MaxValue ? 0f : minRange,
                maxRange = maxRange,
                fireDistance = fireDistance,
                agentShots = agentShots,
                oppShots = oppShots,
                engagements = engagements,
                agentRegenEventsEngaged = agentRegen.EngagedEvents,
                agentRegenAmountEngaged = agentRegen.EngagedAmount,
                agentRegenEventsDisengaged = agentRegen.DisengagedEvents,
                agentRegenAmountDisengaged = agentRegen.DisengagedAmount,
                oppRegenEventsEngaged = oppRegen.EngagedEvents,
                oppRegenAmountEngaged = oppRegen.EngagedAmount,
                oppRegenEventsDisengaged = oppRegen.DisengagedEvents,
                oppRegenAmountDisengaged = oppRegen.DisengagedAmount,
                agentBoostsTrailingGap = agentBoostsGap,
                oppBoostsTrailingGap = oppBoostsGap,
            };
        }

        public void Dispose()
        {
            foreach (var weapon in agentWeapons)
                if (weapon) weapon.OnFire -= CountAgentShot;
            foreach (var weapon in oppWeapons)
                if (weapon) weapon.OnFire -= CountOppShot;
            if (agent) agent.Damage.Shield.OnValueChanged -= OnAgentShieldChanged;
            if (baseline) baseline.Damage.Shield.OnValueChanged -= OnOppShieldChanged;
        }

        private void OpenEngagement(float time, in CombatSnapshot snapshot)
        {
            open = new CombatEngagementRow
            {
                entrySimSeconds = time,
                agentShieldPct = agent.ShieldPct,
                agentHeatPct = agentHeat?.HeatPct ?? 0f,
                agentPoolPct = snapshot.myMaxPool > 0f ? snapshot.myPool / snapshot.myMaxPool : 0f,
                oppShieldPct = baseline.ShieldPct,
                oppHeatPct = oppHeat?.HeatPct ?? 0f,
                oppPoolPct = snapshot.enemyMaxPool > 0f ? snapshot.enemyPool / snapshot.enemyMaxPool : 0f,
                agentBoostsGapBefore = agentBoostsGap,
                oppBoostsGapBefore = oppBoostsGap,
            };
            agentBoostsGap = 0;
            oppBoostsGap = 0;
        }

        private void CountBoost(ref int engagedCount, ref int gapCount)
        {
            if (tracker.Engaged) engagedCount++;
            else gapCount++;
        }

        private void OnAgentShieldChanged(float value, float previous, float _) =>
            agentRegen.Observe(value, previous, tracker.Engaged);

        private void OnOppShieldChanged(float value, float previous, float _) =>
            oppRegen.Observe(value, previous, tracker.Engaged);

        private void CountAgentShot() => agentShots++;
        private void CountOppShot() => oppShots++;

        private static IHeatReadout ResolvePrimaryHeat(Ship ship)
        {
            foreach (var readout in ship.Weapons.ReadoutContext.Readouts(WeaponSlot.Primary))
                if (readout is IHeatReadout heat)
                    return heat;
            return null;
        }
    }

    /// <summary>The rules-change balance instrument as a session probe: one <see cref="CombatTelemetrySampler"/> per episode, rows grouped by the block's opponent label, and a per-opponent means/counts aggregate as the summary sidecar.</summary>
    public sealed class CombatTelemetryProbe : ISessionProbe
    {
        public const string ProbeName = "combat";

        [Serializable]
        private struct Sidecar
        {
            public CombatTelemetrySummary[] opponents;
        }

        private readonly List<string> order = new();
        private readonly Dictionary<string, List<CombatTelemetryRow>> rowsByOpponent = new();
        private CombatTelemetrySampler sampler;
        private string label;

        public string Name => ProbeName;

        public void Begin(in ProbeContext context)
        {
            label = context.opponentLabel;
            sampler = new CombatTelemetrySampler(context.pair, context.arenaCenter, in context.draw);
        }

        public void Sample() => sampler.Sample();

        public string End(in EpisodeResult result)
        {
            var row = sampler.ToRow(in result, label);
            if (!rowsByOpponent.TryGetValue(label, out var rows))
            {
                rows = new List<CombatTelemetryRow>();
                rowsByOpponent[label] = rows;
                order.Add(label);
            }
            rows.Add(row);
            sampler.Dispose();
            sampler = null;
            return row.ToJsonLine();
        }

        public void Summarize(string summaryPath)
        {
            var sidecar = new Sidecar { opponents = new CombatTelemetrySummary[order.Count] };
            for (var i = 0; i < order.Count; i++)
                sidecar.opponents[i] = CombatTelemetrySummary.Summarize(order[i], rowsByOpponent[order[i]]);
            File.WriteAllText(summaryPath, JsonUtility.ToJson(sidecar, prettyPrint: true));
        }

        public void Dispose() => sampler?.Dispose();
    }
}
