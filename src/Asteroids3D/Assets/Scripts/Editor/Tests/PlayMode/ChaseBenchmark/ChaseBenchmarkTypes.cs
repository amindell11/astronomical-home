#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Tests.PlayMode.ChaseBenchmark
{
    /// <summary>
    /// One benchmark run's inputs. A "seed" in the sweep is a (start-offset × seed-bias)
    /// pair: the asteroid field is deterministic and effectively infinite, so flying the
    /// chase at a different absolute offset samples a different obstacle neighbourhood
    /// without mutating the field seed; <see cref="seedBias"/> then varies the MPC
    /// sampler noise deterministically (see <c>SolverBuffers.SeedBias</c>).
    /// </summary>
    [Serializable]
    public struct ChaseRunConfig
    {
        public int index;
        public string label;
        public Vector2 startOffset;   // absolute plane offset of the pursuer start
        public float startGap;        // evader starts this far ahead of the pursuer (+x)
        public float desiredRange;    // pursuer MaintainRange band centre
        public float rangeTolerance;
        public uint seedBias;         // folded into the MPC sampler seed for repeatability
        public int ticks;             // fixed-update steps to simulate

        public static ChaseRunConfig Default(int index) => new ChaseRunConfig
        {
            index = index,
            label = $"run{index}",
            startOffset = Vector2.zero,
            startGap = 24f,
            desiredRange = 6f,
            rangeTolerance = 3f,
            seedBias = (uint)(index * 2654435761u + 1u),
            ticks = 800,
        };
    }

    /// <summary>Per-ship, per-run headline metrics (robust to the shared-MPC confound).</summary>
    [Serializable]
    public struct ShipRunMetrics
    {
        public string role;           // "pursuer" | "evader"
        public int collisions;        // asteroid contacts entered
        public float impactImpulse;   // summed |collision.impulse| over the run
        public float meanSpeed;       // mean plane speed (u/s)
        public float chatterPerSec;   // mean per-second Σ|Δu| across thrust/strafe/yaw (thrash detector)
        public float meanSolveMs;     // mean MPC solve time per tick (editor timing)
    }

    /// <summary>A full run row: config + both ships' metrics + secondary relational context.</summary>
    [Serializable]
    public struct ChaseRunResult
    {
        public int index;
        public string label;
        public float startOffsetX;
        public float startOffsetY;
        public uint seedBias;
        public int ticks;
        public float simSeconds;

        public ShipRunMetrics pursuer;
        public ShipRunMetrics evader;

        // Secondary context (confounded — evader changes build-to-build too).
        public float minDistance;         // closest pursuer→evader approach
        public float meanDistanceBehind;  // mean pursuer→evader distance
        public float timeToInterceptSec;  // first time within interceptRadius, NaN if never

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }
}
#endif
