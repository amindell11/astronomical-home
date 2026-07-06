using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Movement.MPC
{
    [BurstCompile]
    public struct GenerateCandidatesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Control> warmStart;
        [NativeDisableParallelForRestriction]
        public NativeArray<Control> candidates;

        public int horizon;
        public float noiseStd;
        public float boostSampleProbability;
        public uint rngSeed;
        // Number of interpolated knots per channel for time-correlated noise. Min 2.
        public int noiseKnots;

        // Max knots supported without a heap allocation inside the Burst job. Draws beyond
        // this are clamped; 4-5 knots over ~17 steps is the intended operating point.
        private const int MaxKnots = 16;

        public void Execute(int candidateIndex)
        {
            var offset = candidateIndex * horizon;

            if (candidateIndex == 0)
            {
                for (var j = 0; j < horizon; j++)
                    candidates[offset + j] = warmStart[j];
                return;
            }

            var rng = new Unity.Mathematics.Random(rngSeed + (uint)candidateIndex);

            // Time-correlated noise: draw K knot values per channel up front, then linearly
            // interpolate across the horizon. A single draw holds a coherent maneuver over
            // ~horizon/K steps, so candidates can express "hold hard strafe for 0.5 s".
            var k = math.clamp(noiseKnots, 2, MaxKnots);
            var thrustKnots = new float4x4();
            var strafeKnots = new float4x4();
            var yawKnots = new float4x4();
            for (var i = 0; i < k; i++)
            {
                thrustKnots[i / 4][i % 4] = NextGaussian(ref rng);
                strafeKnots[i / 4][i % 4] = NextGaussian(ref rng);
                yawKnots[i / 4][i % 4] = NextGaussian(ref rng);
            }

            var invHorizon = 1f / math.max(1, horizon - 1);
            for (var j = 0; j < horizon; j++)
            {
                var warm = warmStart[j];
                var t = j * (k - 1) * invHorizon;
                var lo = (int)t;
                var frac = t - lo;
                var hi = math.min(lo + 1, k - 1);

                var thrustDelta = math.lerp(Knot(thrustKnots, lo), Knot(thrustKnots, hi), frac) * noiseStd;
                var strafeDelta = math.lerp(Knot(strafeKnots, lo), Knot(strafeKnots, hi), frac) * noiseStd;
                var yawDelta = math.lerp(Knot(yawKnots, lo), Knot(yawKnots, hi), frac) * noiseStd;

                candidates[offset + j] = new Control
                {
                    thrust = math.clamp(warm.thrust + thrustDelta, -1f, 1f),
                    strafe = math.clamp(warm.strafe + strafeDelta, -1f, 1f),
                    yawTorque = math.clamp(warm.yawTorque + yawDelta, -1f, 1f),
                    boost = rng.NextFloat() < boostSampleProbability ? 1f : 0f
                };
            }
        }

        private static float Knot(float4x4 knots, int i) => knots[i / 4][i % 4];

        private static float NextGaussian(ref Unity.Mathematics.Random rng)
        {
            var u1 = 1f - rng.NextFloat();
            var u2 = 1f - rng.NextFloat();
            return math.sqrt(-2f * math.log(u1)) * math.sin(2f * math.PI * u2);
        }
    }

    [BurstCompile]
    public struct EvaluateCandidatesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Control> candidates;
        public NativeArray<float> costs;

        [ReadOnly] public CostInput costInput;
        public State initialState;
        public Config cfg;
        public Dynamics dynamics;
        public Control lastControl;

        public void Execute(int candidateIndex)
        {
            var horizon = cfg.horizon;
            var offset = candidateIndex * horizon;

            var totalCost = 0f;
            var current = initialState;
            var prevU = lastControl;

            for (var i = 0; i < horizon; i++)
            {
                var u = candidates[offset + i];
                var isTerminal = i == horizon - 1;
                totalCost += Cost.Evaluate(current, u, prevU, costInput, cfg, isTerminal, i);
                current = Model.Step(current, u, cfg, dynamics);
                prevU = u;
            }

            costs[candidateIndex] = totalCost;
        }
    }

    /// <summary>
    /// Owns the NativeArray buffers for the Burst solver.
    /// Create once, call Solve each frame, Dispose on teardown.
    /// </summary>
    public class SolverBuffers : System.IDisposable
    {
        /// <summary>
        /// Fixed base for the per-tick sampler seed. Default 0 preserves the production
        /// <c>Time.frameCount</c>-derived seeding <em>exactly</em>. When set non-zero it
        /// replaces the frame-count term (the position hash still varies the seed per ship
        /// and per step), so a run no longer depends on when it started — giving reproducible
        /// single debug runs on top of the statistical (multi-seed) evaluation. Only ever
        /// assigned by the chase-benchmark harness; shipped gameplay leaves it 0.
        /// See doc/Feature_Plans/Chase_Nav_Track_B_Field_And_Eval.md.
        /// </summary>
        public static uint SeedBias;

        private NativeArray<Control> warmStart;
        private NativeArray<Control> candidates;
        private NativeArray<float> costs;
        private NativeArray<Control> result;
        private NativeArray<ObstacleData> obstacles;
        private NativeArray<State> enemyStates;
        private bool allocated;
        private int lastObstacleCount;

        public NativeArray<ObstacleData> Obstacles => obstacles;
        public int ObstacleCount => lastObstacleCount;
        public NativeArray<State> EnemyStates => enemyStates;
        public int LastEnemyStateCount { get; private set; }

        // Editor visualization access — read-only candidate sequences and their costs
        // from the most recent Solve(). Valid until the next Solve() rewrites the buffers.
        public NativeArray<Control> Candidates => candidates;
        public NativeArray<float> Costs => costs;
        public int LastSampleCount { get; private set; }
        public int LastHorizon { get; private set; }

        public float Solve(State initialState, Control[] sequence,
            Config cfg, Dynamics dynamics,
            AI.Scanning.ObstacleScan scan, bool useObstacles,
            float2 goalPos, float2 goalVel,
            float2 enemyPos, float2 enemyVel, float enemyYaw, float enemyYawRate,
            Dynamics enemyDynamics, float projectileSpeed,
            int samples, float noiseStd, Control lastControl,
            float boostCooldownRemaining = 0f, float boostSampleProbability = 0.15f,
            float eliteFraction = 0.1f, int noiseKnots = 4)
        {
            var horizon = cfg.horizon;
            EnsureBuffers(horizon, samples);
            LastSampleCount = samples;
            LastHorizon = horizon;

            for (var i = 0; i < horizon; i++)
                warmStart[i] = sequence[i];

            ConvertObstacles(scan, useObstacles, dynamics.mass);

            // Roll out predicted enemy trajectory assuming maintained thrust along facing
            var hasEnemyRollout = !math.isnan(enemyYaw) && enemyDynamics.mass > 0f;
            var enemyStateCount = 0;
            if (hasEnemyRollout)
            {
                var enemyState = new State
                {
                    pos = enemyPos,
                    vel = enemyVel,
                    yaw = enemyYaw,
                    yawRate = enemyYawRate,
                };
                // Estimate thrust and strafe from velocity projected onto body axes
                var sin = math.sin(enemyYaw);
                var cos = math.cos(enemyYaw);
                var fwd = new float2(-sin, cos);
                var right = new float2(cos, sin);
                var estimatedThrust = math.clamp(math.dot(enemyVel, fwd) / enemyDynamics.maxSpeed, -1f, 1f);
                var estimatedStrafe = math.clamp(math.dot(enemyVel, right) / enemyDynamics.maxSpeed, -1f, 1f);
                var enemyControl = new Control { thrust = estimatedThrust, strafe = estimatedStrafe };
                for (var i = 0; i < horizon; i++)
                {
                    enemyState = Model.Step(enemyState, enemyControl, cfg, enemyDynamics);
                    enemyStates[i] = enemyState;
                }
                enemyStateCount = horizon;
            }
            LastEnemyStateCount = enemyStateCount;

            var costInput = new CostInput
            {
                goalPos = goalPos,
                goalVel = goalVel,
                obstacles = obstacles,
                obstacleCount = lastObstacleCount,
                enemyPos = enemyPos,
                enemyVel = enemyVel,
                enemyYaw = enemyYaw,
                enemyYawRate = enemyYawRate,
                projectileSpeed = projectileSpeed,
                enemyStates = enemyStates,
                enemyStateCount = enemyStateCount,
                initialVel = initialState.vel,
            };

            initialState.boostCooldownRemaining = boostCooldownRemaining;

            var frameComponent = SeedBias != 0u ? SeedBias : (uint)(Time.frameCount * 7919);
            var rngSeed = frameComponent + (uint)initialState.pos.GetHashCode();
            if (rngSeed == 0) rngSeed = 1;

            new GenerateCandidatesJob
            {
                warmStart = warmStart,
                candidates = candidates,
                horizon = horizon,
                noiseStd = noiseStd,
                boostSampleProbability = boostSampleProbability,
                rngSeed = rngSeed,
                noiseKnots = noiseKnots
            }.Schedule(samples, 1).Complete();

            Evaluate(initialState, costInput, cfg, dynamics, lastControl, samples);

            return EliteAverage(sequence, cfg.horizon, samples, eliteFraction);
        }

        /// <summary>
        /// Re-evaluate the candidates from the last Solve() with a different config.
        /// Reuses the same control sequences — only cost evaluation and elite selection differ.
        /// Must be called after Solve() while buffers are still valid.
        /// </summary>
        public float Rescore(State initialState, Control[] sequence,
            Config cfg, Dynamics dynamics, CostInput costInput,
            Control lastControl, int samples, float eliteFraction = 0.1f)
        {
            if (!allocated) return float.MaxValue;
            Evaluate(initialState, costInput, cfg, dynamics, lastControl, samples);
            return EliteAverage(sequence, cfg.horizon, samples, eliteFraction);
        }

        private void Evaluate(State initialState, CostInput costInput,
            Config cfg, Dynamics dynamics, Control lastControl, int samples)
        {
            new EvaluateCandidatesJob
            {
                candidates = candidates,
                costs = costs,
                costInput = costInput,
                initialState = initialState,
                cfg = cfg,
                dynamics = dynamics,
                lastControl = lastControl,
            }.Schedule(samples, 1).Complete();
        }

        private float EliteAverage(Control[] sequence, int horizon, int samples, float eliteFraction)
        {
            var eliteCount = math.max(1, (int)(samples * math.clamp(eliteFraction, 0.01f, 0.5f)));
            var costThreshold = FindKthSmallest(costs, samples, eliteCount);

            for (var j = 0; j < horizon; j++)
                result[j] = default;

            var bestCost = float.MaxValue;
            var counted = 0;
            for (var i = 0; i < samples && counted < eliteCount; i++)
            {
                if (costs[i] > costThreshold) continue;
                if (costs[i] < bestCost) bestCost = costs[i];

                var offset = i * horizon;
                for (var j = 0; j < horizon; j++)
                {
                    var c = result[j];
                    var s = candidates[offset + j];
                    result[j] = new Control
                    {
                        thrust = c.thrust + s.thrust,
                        strafe = c.strafe + s.strafe,
                        yawTorque = c.yawTorque + s.yawTorque,
                        boost = c.boost + s.boost,
                    };
                }
                counted++;
            }

            var invCount = 1f / counted;
            for (var j = 0; j < horizon; j++)
            {
                var c = result[j];
                sequence[j] = new Control
                {
                    thrust = math.clamp(c.thrust * invCount, -1f, 1f),
                    strafe = math.clamp(c.strafe * invCount, -1f, 1f),
                    yawTorque = math.clamp(c.yawTorque * invCount, -1f, 1f),
                    boost = c.boost * invCount > 0.5f ? 1f : 0f,
                };
            }

            return bestCost;
        }

        /// <summary>
        /// Find the K-th smallest value in costs[0..count-1] using a single pass.
        /// Returns a threshold: at least K elements have cost &lt;= this value.
        /// </summary>
        private static float FindKthSmallest(NativeArray<float> costs, int count, int k)
        {
            if (k >= count) return float.MaxValue;

            var heap = new NativeArray<float>(k, Allocator.Temp);
            for (var i = 0; i < k; i++)
                heap[i] = costs[i];

            for (var i = k / 2 - 1; i >= 0; i--)
                SiftDown(heap, i, k);

            for (var i = k; i < count; i++)
            {
                if (costs[i] < heap[0])
                {
                    heap[0] = costs[i];
                    SiftDown(heap, 0, k);
                }
            }

            var result = heap[0];
            heap.Dispose();
            return result;
        }

        private static void SiftDown(NativeArray<float> heap, int i, int n)
        {
            while (true)
            {
                var largest = i;
                var left = 2 * i + 1;
                var right = 2 * i + 2;
                if (left < n && heap[left] > heap[largest]) largest = left;
                if (right < n && heap[right] > heap[largest]) largest = right;
                if (largest == i) break;
                (heap[i], heap[largest]) = (heap[largest], heap[i]);
                i = largest;
            }
        }

        public CostInput BuildCostInput(float2 goalPos, float2 goalVel = default,
            float2 enemyPos = default, float2 enemyVel = default,
            float enemyYaw = float.NaN, float enemyYawRate = 0f, float projectileSpeed = 0f,
            float2 initialVel = default)
        {
            return new CostInput
            {
                goalPos = goalPos,
                goalVel = goalVel,
                obstacles = obstacles,
                obstacleCount = lastObstacleCount,
                enemyPos = enemyPos,
                enemyVel = enemyVel,
                enemyYaw = enemyYaw,
                enemyYawRate = enemyYawRate,
                projectileSpeed = projectileSpeed,
                enemyStates = enemyStates.IsCreated ? enemyStates : default,
                enemyStateCount = enemyStates.IsCreated ? enemyStates.Length : 0,
                initialVel = initialVel,
            };
        }

        private void ConvertObstacles(AI.Scanning.ObstacleScan scan, bool useObstacles,
            float shipMass)
        {
            // Clamp so a merged obstacle+ship scan can't overflow the fixed-size native buffer.
            var rawCount = (scan.count > 0 && useObstacles) ? scan.count : 0;
            lastObstacleCount = math.min(rawCount, obstacles.Length);
            var invShipMass = shipMass > 0f ? 1f / shipMass : 1f;
            for (var i = 0; i < lastObstacleCount; i++)
            {
                var obs = scan.buffer[i];
                var rb = obs.collider ? obs.collider.attachedRigidbody : null;
                var obsMass = rb ? rb.mass : shipMass;
                obstacles[i] = new ObstacleData
                {
                    // True obstacle radius. The ship footprint (hull = shipRadius * bank profile)
                    // now lives in the cost evaluation, not baked into the obstacle here.
                    position = new float2(obs.position.x, obs.position.y),
                    radius = obs.radius,
                    weight = obsMass * invShipMass
                };
            }
        }

        private void EnsureBuffers(int horizon, int samples)
        {
            if (allocated && warmStart.Length == horizon && costs.Length == samples)
                return;
            Dispose();
            warmStart = new NativeArray<Control>(horizon, Allocator.Persistent);
            candidates = new NativeArray<Control>(samples * horizon, Allocator.Persistent);
            costs = new NativeArray<float>(samples, Allocator.Persistent);
            obstacles = new NativeArray<ObstacleData>(64, Allocator.Persistent);
            enemyStates = new NativeArray<State>(horizon, Allocator.Persistent);
            result = new NativeArray<Control>(horizon, Allocator.Persistent);
            allocated = true;
        }

        public void Dispose()
        {
            if (!allocated) return;
            warmStart.Dispose();
            candidates.Dispose();
            costs.Dispose();
            obstacles.Dispose();
            enemyStates.Dispose();
            result.Dispose();
            allocated = false;
        }
    }
}
