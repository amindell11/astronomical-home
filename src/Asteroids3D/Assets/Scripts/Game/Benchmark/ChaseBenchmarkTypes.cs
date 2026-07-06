using System;
using UnityEngine;

namespace Game.Benchmark
{
    /// <summary>
    /// One benchmark episode's configuration. Headless runs push this through
    /// <see cref="ChaseBenchmarkModule.PendingConfig"/> before sector Setup; watchable
    /// (in-editor) play uses the module's serialized defaults instead.
    /// </summary>
    [Serializable]
    public struct ChaseRunConfig
    {
        [Tooltip("Deterministic layout seed applied to the asteroid field before it initializes.")]
        public int fieldSeed;

        [Tooltip("Plane-space translation applied to the asteroid field element, shifting the " +
                 "layout under the (fixed) ship start positions — the 'start offset' axis of the " +
                 "seed sweep.")]
        public Vector2 fieldOffset;

        [Tooltip("Episode length in simulated seconds. 0 = endless (watchable mode).")]
        public float duration;

        [Tooltip("When true, pins the MPC sampler RNG via SolverBuffers.SamplerSeedOverride for " +
                 "reproducible single debug runs. Aggregate benchmarking leaves this off and " +
                 "treats runs statistically.")]
        public bool pinSamplerSeed;

        [Tooltip("Base sampler seed used when pinSamplerSeed is set.")]
        public uint samplerSeed;

        [Tooltip("Free-form tag copied into the result row (e.g. 'baseline-main').")]
        public string tag;
    }

    /// <summary>Per-ship headline metrics for one episode (robust to the shared-MPC confound).</summary>
    [Serializable]
    public struct ChaseShipMetrics
    {
        public int collisions;
        [Tooltip("Sum of collision impulse magnitudes (N*s) over the episode.")]
        public float impactImpulse;
        public float meanSpeed;
        [Tooltip("Mean |du| per second, per control axis — the thrash detector.")]
        public float chatterThrust;
        public float chatterStrafe;
        public float chatterYaw;
        [Tooltip("MPC solve time (editor-only measurement; 0 in player builds).")]
        public float meanSolveMs;
        public float maxSolveMs;
    }

    /// <summary>
    /// One JSONL row: the episode config echo, per-ship headline metrics, and the
    /// (confounded, secondary) chase-geometry context.
    /// </summary>
    [Serializable]
    public struct ChaseRunResult
    {
        public string schema;
        public string tag;
        public int fieldSeed;
        public Vector2 fieldOffset;
        public float duration;
        public bool pinnedSampler;

        public ChaseShipMetrics pursuer;
        public ChaseShipMetrics evader;

        // Secondary context — confounded (both ships run the MPC-under-test).
        public float meanDistance;
        public float minDistance;
        public float finalDistance;
        [Tooltip("Seconds the pursuer spent within the contact radius of the evader.")]
        public float contactTime;
        public float contactRadius;

        public const string SchemaId = "chase-bench-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }
}
