using System.Collections;
using AI;
using Asteroids.Fields;
using Game.Sectors;
using Movement.MPC;
using Ships;
using Ships.Damage;
using UnityEngine;

namespace Game.Benchmark
{
    /// <summary>
    /// Behavior module for the two-AI chase benchmark sector: a pursuer (MaintainRange, tight
    /// enemy-anchored band) chasing an evader (Flee) through the dense deterministic asteroid
    /// field. One scenario, two entry points:
    /// <list type="bullet">
    /// <item>Watchable — play the ChaseBenchmarkSector prefab through the normal game flow;
    /// serialized defaults apply (endless episode, authored seed).</item>
    /// <item>Headless — the ChaseBenchmark PlayMode test pushes a <see cref="ChaseRunConfig"/>
    /// through <see cref="PendingConfig"/> before Setup, runs the episode to completion and
    /// serializes <see cref="LastResult"/> to JSONL.</item>
    /// </list>
    /// Setup runs while the sector is still under its inactive load holder, i.e. before the
    /// field's Start — so the layout seed / field offset are applied purely declaratively; the
    /// field then initializes once with them. The module also re-anchors chunk streaming to the
    /// evader (the chase is the subject, not the player) and attaches a
    /// <see cref="ChaseMetricsProbe"/> to each ship.
    /// </summary>
    public class ChaseBenchmarkModule : SectorModule
    {
        [Header("Scenario wiring (prefab references)")]
        [SerializeField] private Ship pursuer;
        [SerializeField] private Ship evader;
        [SerializeField] private AICommander pursuerPilot;
        [SerializeField] private AICommander evaderPilot;
        [SerializeField] private UpdatingAsteroidField field;

        [Header("Episode defaults (headless runs override via PendingConfig)")]
        [Tooltip("Layout seed applied to the field before it initializes.")]
        [SerializeField] private int fieldSeed = 20260705;
        [Tooltip("Plane-space translation of the field element (start-offset sweep axis).")]
        [SerializeField] private Vector2 fieldOffset = Vector2.zero;
        [Tooltip("Episode length in sim seconds; 0 = endless (watchable mode).")]
        [SerializeField] private float episodeDuration;
        [Tooltip("Pursuer counts as 'in contact' within this plane distance of the evader.")]
        [SerializeField] private float contactRadius = 15f;
        [Tooltip("Keep both ships alive by resetting damage each tick (impacts still recorded).")]
        [SerializeField] private bool keepShipsAlive = true;

        /// <summary>Set by the headless runner before sector Setup; consumed (cleared) by Setup.</summary>
        public static ChaseRunConfig? PendingConfig;

        private ChaseRunConfig activeConfig;
        private ChaseMetricsProbe pursuerProbe;
        private ChaseMetricsProbe evaderProbe;
        private bool running;
        private float simTime;
        private float distanceSum;
        private int distanceSamples;
        private float minDistance = float.MaxValue;
        private float lastDistance;
        private float contactTime;

        /// <summary>Completed-episode result; valid once <see cref="EpisodeComplete"/> is true.</summary>
        public ChaseRunResult LastResult { get; private set; }
        public bool EpisodeComplete { get; private set; }

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            activeConfig = PendingConfig ?? new ChaseRunConfig
            {
                fieldSeed = fieldSeed,
                fieldOffset = fieldOffset,
                duration = episodeDuration,
                pinSamplerSeed = false,
                samplerSeed = 0,
                tag = "watchable",
            };
            PendingConfig = null;

            if (field)
            {
                // Pre-Start: purely declarative — the field initializes once with these.
                field.SetLayoutSeed(activeConfig.fieldSeed);
                field.transform.position += GamePlane.PlaneDirToWorld(activeConfig.fieldOffset);
                // Chunk streaming follows the chase, not the (possibly absent) player.
                if (evader) field.SetAnchor(evader.transform);
            }

            SolverBuffers.SamplerSeedOverride =
                activeConfig.pinSamplerSeed ? activeConfig.samplerSeed : null;

            pursuerProbe = AttachProbe(pursuer, pursuerPilot);
            evaderProbe = AttachProbe(evader, evaderPilot);

            // Reset per-episode accumulators: a sector restart reuses this module instance,
            // and stale sums/durations would end the next episode instantly or pollute its row.
            simTime = 0f;
            distanceSum = 0f;
            distanceSamples = 0;
            minDistance = float.MaxValue;
            lastDistance = 0f;
            contactTime = 0f;
            LastResult = default;

            running = true;
            EpisodeComplete = false;
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            SolverBuffers.SamplerSeedOverride = null;
            running = false;
            if (pursuerProbe) Destroy(pursuerProbe);
            if (evaderProbe) Destroy(evaderProbe);
            yield break;
        }

        private ChaseMetricsProbe AttachProbe(Ship ship, AICommander pilot)
        {
            if (!ship || !pilot) return null;
            var probe = ship.gameObject.AddComponent<ChaseMetricsProbe>();
            probe.Initialize(ship, pilot.Navigator, ship.Damage, keepShipsAlive);
            return probe;
        }

        private void FixedUpdate()
        {
            if (!running || !pursuer || !evader) return;

            simTime += Time.fixedDeltaTime;
            lastDistance = Vector2.Distance(pursuer.Kinematics.pos, evader.Kinematics.pos);
            distanceSum += lastDistance;
            distanceSamples++;
            if (lastDistance < minDistance) minDistance = lastDistance;
            if (lastDistance <= contactRadius) contactTime += Time.fixedDeltaTime;

            if (activeConfig.duration > 0f && simTime >= activeConfig.duration)
                FinishEpisode();
        }

        private void FinishEpisode()
        {
            running = false;
            LastResult = new ChaseRunResult
            {
                schema = ChaseRunResult.SchemaId,
                tag = activeConfig.tag,
                fieldSeed = activeConfig.fieldSeed,
                fieldOffset = activeConfig.fieldOffset,
                duration = simTime,
                pinnedSampler = activeConfig.pinSamplerSeed,
                pursuer = pursuerProbe ? pursuerProbe.Snapshot() : default,
                evader = evaderProbe ? evaderProbe.Snapshot() : default,
                meanDistance = distanceSamples > 0 ? distanceSum / distanceSamples : 0f,
                minDistance = distanceSamples > 0 ? minDistance : 0f,
                finalDistance = lastDistance,
                contactTime = contactTime,
                contactRadius = contactRadius,
            };
            EpisodeComplete = true;
        }
    }
}
