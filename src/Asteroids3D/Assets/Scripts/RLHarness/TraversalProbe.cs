using System;
using System.Collections.Generic;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One traversal crossing's config, embedded verbatim in every result row so the JSONL is self-describing. The sweep axes (densityScale × speedFraction × layoutSeed × MpcSettings overrides) all live here; <c>driver</c> tags which <see cref="AI.IIntentChooser"/> flew the crossing.</summary>
    [Serializable]
    public struct TraversalSpec
    {
        public int runSeed;
        public string driver;
        public float crossingRadius;
        public float densityScale;
        public float speedFraction;
        public int layoutSeed;
        public float wVelTrack;
        public float timeoutFactor;

        public static TraversalSpec Default => new()
        {
            runSeed = 1,
            driver = VelocityTraversalChooser.DriverTag,
            crossingRadius = 120f,
            densityScale = 1f,
            speedFraction = 0.9f,
            layoutSeed = 1,
            wVelTrack = 50f,
            timeoutFactor = 4f,
        };
    }

    public enum TraversalOutcome { Unresolved, Arrived, Died, Timeout }

    [Serializable]
    public struct TraversalResult
    {
        public string schema;
        public int episodeIndex;
        public string outcome;
        public float simSeconds;
        public float alongTrack;
        public float effectiveSpeed;
        public int collisionEvents;
        public float collisionDamage;
        public float endHealthPct;
        public float endShieldPct;
        public TraversalSpec spec;

        public const string SchemaId = "rl-traversal-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>Per-(driver, density, speed) aggregate over a sweep cell — the go/no-go curve points (completion rate, effective traversal speed, collision load).</summary>
    [Serializable]
    public struct TraversalSummary
    {
        public string schema;
        public string driver;
        public float densityScale;
        public float speedFraction;
        public float wVelTrack;
        public int episodes;
        public int arrived;
        public int died;
        public int timedOut;
        public float completionRate;
        public float meanEffectiveSpeed;
        public float meanCollisionEvents;
        public float meanCollisionDamage;

        public const string SchemaId = "rl-traversal-summary-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);

        public static TraversalSummary Summarize(in TraversalSpec cell, IReadOnlyList<TraversalResult> rows)
        {
            var arrived = 0;
            var died = 0;
            var timedOut = 0;
            var arrivedSpeedSum = 0f;
            var collisionSum = 0f;
            var damageSum = 0f;
            foreach (var row in rows)
            {
                if (row.outcome == TraversalOutcome.Arrived.ToString())
                {
                    arrived++;
                    arrivedSpeedSum += row.effectiveSpeed;
                }
                else if (row.outcome == TraversalOutcome.Died.ToString()) died++;
                else if (row.outcome == TraversalOutcome.Timeout.ToString()) timedOut++;
                collisionSum += row.collisionEvents;
                damageSum += row.collisionDamage;
            }
            return new TraversalSummary
            {
                schema = SchemaId,
                driver = cell.driver,
                densityScale = cell.densityScale,
                speedFraction = cell.speedFraction,
                wVelTrack = cell.wVelTrack,
                episodes = rows.Count,
                arrived = arrived,
                died = died,
                timedOut = timedOut,
                completionRate = rows.Count > 0 ? (float)arrived / rows.Count : 0f,
                meanEffectiveSpeed = arrived > 0 ? arrivedSpeedSum / arrived : 0f,
                meanCollisionEvents = rows.Count > 0 ? collisionSum / rows.Count : 0f,
                meanCollisionDamage = rows.Count > 0 ? damageSum / rows.Count : 0f,
            };
        }
    }

    /// <summary>Pure crossing geometry from (runSeed, layoutSeed): a diameter of the field disc — edge spawn, opposite-edge destination — so every (seed, density) cell flies a reproducible but rotated line.</summary>
    public static class TraversalCrossing
    {
        private const uint BearingStream = 404;

        public static void Derive(in TraversalSpec spec, Vector2 center,
            out Vector2 start, out Vector2 destination, out Vector2 dir)
        {
            var rng = new System.Random(
                new SeedScope(spec.runSeed).Derive(BearingStream).Derive((uint)spec.layoutSeed).ToSeed());
            var bearing = (float)(rng.NextDouble() * 2.0 * Math.PI);
            dir = new Vector2(Mathf.Cos(bearing), Mathf.Sin(bearing));
            start = center - spec.crossingRadius * dir;
            destination = center + spec.crossingRadius * dir;
        }
    }

    /// <summary>Host-agnostic single-crossing loop beside <see cref="EpisodeRunner"/>: the driver calls <see cref="Tick"/> once per fixed step; the runner owns termination (arrival = full-diameter along-track progress, death, timeout) and the collision ledger (damage events on a weapons-silent crossing are collisions by construction).</summary>
    public sealed class TraversalRunner
    {
        private readonly Ship ship;
        private readonly Vector2 start;
        private readonly Vector2 dir;
        private readonly float crossingDistance;
        private readonly float maxSimSeconds;

        private int steps;
        private bool dead;
        private int collisionEvents;
        private float collisionDamage;
        private float alongTrack;
        private TraversalResult result;

        public bool IsDone { get; private set; }
        public TraversalResult Result => result;

        public TraversalRunner(Ship ship, in TraversalSpec spec, int episodeIndex, Vector2 start, Vector2 dir)
        {
            this.ship = ship;
            this.start = start;
            this.dir = dir;
            crossingDistance = 2f * spec.crossingRadius;
            maxSimSeconds = spec.timeoutFactor * crossingDistance
                / Mathf.Max(0.01f, spec.speedFraction * ship.Dynamics.maxSpeed);
            result = new TraversalResult
            {
                schema = TraversalResult.SchemaId,
                episodeIndex = episodeIndex,
                outcome = TraversalOutcome.Unresolved.ToString(),
                spec = spec,
            };
        }

        public void Begin()
        {
            ship.Damage.OnDamaged += HandleDamaged;
            ship.Damage.OnDeath += HandleDeath;
        }

        /// <summary>Advance one fixed step; returns true when the crossing ended this tick.</summary>
        public bool Tick()
        {
            if (IsDone) return false;
            steps++;
            var simSeconds = steps * Time.fixedDeltaTime;
            if (ship)
                alongTrack = Vector2.Dot(ship.Kinematics.pos - start, dir);

            if (dead) Finish(TraversalOutcome.Died, simSeconds);
            else if (alongTrack >= crossingDistance) Finish(TraversalOutcome.Arrived, simSeconds);
            else if (simSeconds >= maxSimSeconds) Finish(TraversalOutcome.Timeout, simSeconds);
            return IsDone;
        }

        private void HandleDamaged(float damage, Vector3 hitPoint)
        {
            collisionEvents++;
            collisionDamage += damage;
        }

        private void HandleDeath(ShipId victim, ShipId killer) => dead = true;

        private void Finish(TraversalOutcome outcome, float simSeconds)
        {
            ship.Damage.OnDamaged -= HandleDamaged;
            ship.Damage.OnDeath -= HandleDeath;
            result.outcome = outcome.ToString();
            result.simSeconds = simSeconds;
            result.alongTrack = alongTrack;
            result.effectiveSpeed = simSeconds > 0f ? alongTrack / simSeconds : 0f;
            result.collisionEvents = collisionEvents;
            result.collisionDamage = collisionDamage;
            result.endHealthPct = ship ? ship.HealthPct : 0f;
            result.endShieldPct = ship ? ship.ShieldPct : 0f;
            IsDone = true;
        }
    }
}
