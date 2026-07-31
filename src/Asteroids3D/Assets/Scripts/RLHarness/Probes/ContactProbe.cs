using System;
using System.Collections.Generic;
using System.IO;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One contact-probe episode's ship-ship contact signals; contactRange records the geometric threshold the episode measured at.</summary>
    [Serializable]
    public struct ContactRow
    {
        public string schema;
        public int seed;
        public int episodeIndex;
        public string opponent;
        public string outcome;
        public float simSeconds;
        public bool mutualKill;
        public float agentHpLostPct;
        public float oppHpLostPct;
        public float longestContactSec;
        public float contactFraction;
        public float minRange;
        public float meanTumbleInContact;
        public float contactRange;

        public const string SchemaId = "rl-contact-probe-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>Per-opponent aggregate over a contact sweep — the ram bench's ConditionAgg shape.</summary>
    [Serializable]
    public struct ContactSummary
    {
        public string schema;
        public string opponent;
        public int episodes;
        public float meanLongestContactSec;
        public float maxLongestContactSec;
        public float meanContactFraction;
        public float meanMinRange;
        public float meanTtk;
        public float mutualKillRate;
        public float meanAgentHpLostPct;
        public float meanOppHpLostPct;
        public float meanTumbleInContact;

        public const string SchemaId = "rl-contact-probe-summary-v1";

        public static ContactSummary Summarize(string opponent, IReadOnlyList<ContactRow> rows)
        {
            var summary = new ContactSummary { schema = SchemaId, opponent = opponent, episodes = rows.Count };
            foreach (var row in rows)
            {
                summary.meanLongestContactSec += row.longestContactSec;
                summary.maxLongestContactSec = Mathf.Max(summary.maxLongestContactSec, row.longestContactSec);
                summary.meanContactFraction += row.contactFraction;
                summary.meanMinRange += row.minRange;
                summary.meanTtk += row.simSeconds;
                if (row.mutualKill) summary.mutualKillRate += 1f;
                summary.meanAgentHpLostPct += row.agentHpLostPct;
                summary.meanOppHpLostPct += row.oppHpLostPct;
                summary.meanTumbleInContact += row.meanTumbleInContact;
            }
            if (rows.Count > 0)
            {
                var n = rows.Count;
                summary.meanLongestContactSec /= n;
                summary.meanContactFraction /= n;
                summary.meanMinRange /= n;
                summary.meanTtk /= n;
                summary.mutualKillRate /= n;
                summary.meanAgentHpLostPct /= n;
                summary.meanOppHpLostPct /= n;
                summary.meanTumbleInContact /= n;
            }
            return summary;
        }
    }

    /// <summary>Per-fixed-step ship-ship contact sampler: longest contiguous in-contact run (sustained contact), overall in-contact fraction, and relative angular velocity (tumble) while touching. Contact = centers within the summed bumper-sphere radii + epsilon. Public so slice E's regression client can drive it directly.</summary>
    public sealed class ContactSampler
    {
        private readonly Ship a;
        private readonly Ship b;
        private readonly float contactRange;
        private int totalSteps;
        private int contactSteps;
        private int longestRun;
        private int currentRun;
        private float tumbleSumInContact;
        private float minRange = float.MaxValue;

        public ContactSampler(EpisodePair pair, float contactRange)
        {
            a = pair.Agent;
            b = pair.Baseline;
            this.contactRange = contactRange;
        }

        public void Sample()
        {
            totalSteps++;
            var range = (a.Kinematics.pos - b.Kinematics.pos).magnitude;
            if (range < minRange) minRange = range;
            if (range <= contactRange)
            {
                contactSteps++;
                currentRun++;
                if (currentRun > longestRun) longestRun = currentRun;
                tumbleSumInContact += Mathf.Abs(a.Kinematics.yawRate - b.Kinematics.yawRate);
            }
            else
            {
                currentRun = 0;
            }
        }

        public ContactRow ToRow(in EpisodeResult result, string opponent) => new()
        {
            schema = ContactRow.SchemaId,
            seed = result.spec.runSeed,
            episodeIndex = result.episodeIndex,
            opponent = opponent,
            outcome = result.outcome,
            simSeconds = result.simSeconds,
            mutualKill = result.mutualKill,
            agentHpLostPct = result.startMyPool > 0f
                ? (result.startMyPool - result.endMyPool) / result.startMyPool : 0f,
            oppHpLostPct = result.startEnemyPool > 0f
                ? (result.startEnemyPool - result.endEnemyPool) / result.startEnemyPool : 0f,
            longestContactSec = longestRun * Time.fixedDeltaTime,
            contactFraction = totalSteps > 0 ? (float)contactSteps / totalSteps : 0f,
            minRange = minRange == float.MaxValue ? 0f : minRange,
            meanTumbleInContact = contactSteps > 0 ? tumbleSumInContact / contactSteps : 0f,
            contactRange = contactRange,
        };
    }

    /// <summary>The ram-bench contact instrument as a session probe: one <see cref="ContactSampler"/> per episode at the pair's bumper-derived contact range, rows grouped by the block's opponent label, and a per-opponent aggregate as the summary sidecar.</summary>
    public sealed class ContactProbe : ISessionProbe
    {
        public const string ProbeName = "contact";
        /// <summary>Tight tolerance on the summed bumper radii — actual touch, not orbit.</summary>
        private const float ContactEpsilon = 0.5f;

        [Serializable]
        private struct Sidecar
        {
            public ContactSummary[] opponents;
        }

        private readonly List<string> order = new();
        private readonly Dictionary<string, List<ContactRow>> rowsByOpponent = new();
        private ContactSampler sampler;
        private string label;
        private Ship agent;
        private float contactRange;

        public string Name => ProbeName;

        public void Begin(in ProbeContext context)
        {
            label = context.opponentLabel;
            // Bumper radius survives respawns; re-derive only when a new composition's pair arrives.
            if (context.pair.Agent != agent)
            {
                agent = context.pair.Agent;
                contactRange = 2f * BumperWorldRadius(agent) + ContactEpsilon;
            }
            sampler = new ContactSampler(context.pair, contactRange);
        }

        public void Sample() => sampler.Sample();

        public string End(in EpisodeResult result)
        {
            var row = sampler.ToRow(in result, label);
            if (!rowsByOpponent.TryGetValue(label, out var rows))
            {
                rows = new List<ContactRow>();
                rowsByOpponent[label] = rows;
                order.Add(label);
            }
            rows.Add(row);
            sampler = null;
            return row.ToJsonLine();
        }

        public void Summarize(string summaryPath)
        {
            var sidecar = new Sidecar { opponents = new ContactSummary[order.Count] };
            for (var i = 0; i < order.Count; i++)
                sidecar.opponents[i] = ContactSummary.Summarize(order[i], rowsByOpponent[order[i]]);
            File.WriteAllText(summaryPath, JsonUtility.ToJson(sidecar, prettyPrint: true));
        }

        public void Dispose() { }

        private static float BumperWorldRadius(Ship ship)
        {
            var bumper = ship.GetComponentInChildren<SphereCollider>();
            if (!bumper)
                throw new InvalidOperationException($"{ProbeName} probe: {ship.name} has no bumper SphereCollider.");
            var scale = bumper.transform.lossyScale;
            return bumper.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
    }
}
