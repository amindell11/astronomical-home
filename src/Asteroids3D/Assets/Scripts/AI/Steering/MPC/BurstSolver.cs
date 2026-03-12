using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Movement.MPC
{
    [BurstCompile]
    public struct EvaluateCandidatesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Control> warmStart;
        [NativeDisableParallelForRestriction]
        public NativeArray<Control> candidates;
        public NativeArray<float> costs;

        [ReadOnly] public CostInput costInput;
        public State initialState;
        public Config cfg;
        public Dynamics dynamics;
        public Control lastControl;
        public float noiseStd;
        public uint rngSeed;

        public void Execute(int candidateIndex)
        {
            var horizon = cfg.horizon;
            var offset = candidateIndex * horizon;

            if (candidateIndex == 0)
            {
                for (var j = 0; j < horizon; j++)
                    candidates[offset + j] = warmStart[j];
            }
            else
            {
                var rng = new Unity.Mathematics.Random(rngSeed + (uint)candidateIndex);
                for (var j = 0; j < horizon; j++)
                {
                    var warm = warmStart[j];
                    candidates[offset + j] = new Control
                    {
                        thrust = math.clamp(warm.thrust + NextGaussian(ref rng) * noiseStd, -1f, 1f),
                        strafe = math.clamp(warm.strafe + NextGaussian(ref rng) * noiseStd, -1f, 1f),
                        yawTorque = math.clamp(warm.yawTorque + NextGaussian(ref rng) * noiseStd, -1f, 1f)
                    };
                }
            }

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

        private static float NextGaussian(ref Unity.Mathematics.Random rng)
        {
            var u1 = 1f - rng.NextFloat();
            var u2 = 1f - rng.NextFloat();
            return math.sqrt(-2f * math.log(u1)) * math.sin(2f * math.PI * u2);
        }
    }

    /// <summary>
    /// Owns the NativeArray buffers for the Burst solver.
    /// Create once, call Solve each frame, Dispose on teardown.
    /// </summary>
    public class SolverBuffers : System.IDisposable
    {
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

        public float Solve(State initialState, Control[] sequence,
            AI.Scanning.ObstacleScan scan, bool useObstacles,
            float2 goalPos, float2 goalVel, float enemyYaw, float enemyYawRate,
            float projectileSpeed, Config cfg, Dynamics dynamics,
            int samples, float noiseStd, Control lastControl,
            Dynamics enemyDynamics = default)
        {
            var horizon = cfg.horizon;
            EnsureBuffers(horizon, samples);

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
                    pos = goalPos,
                    vel = goalVel,
                    yaw = enemyYaw,
                    yawRate = enemyYawRate,
                };
                // Estimate thrust and strafe from velocity projected onto body axes
                var sin = math.sin(enemyYaw);
                var cos = math.cos(enemyYaw);
                var fwd = new float2(-sin, cos);
                var right = new float2(cos, sin);
                var estimatedThrust = math.clamp(math.dot(goalVel, fwd) / enemyDynamics.maxSpeed, -1f, 1f);
                var estimatedStrafe = math.clamp(math.dot(goalVel, right) / enemyDynamics.maxSpeed, -1f, 1f);
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
                enemyYaw = enemyYaw,
                enemyYawRate = enemyYawRate,
                projectileSpeed = projectileSpeed,
                enemyStates = enemyStates,
                enemyStateCount = enemyStateCount,
            };

            var rngSeed = (uint)(Time.frameCount * 7919 + initialState.pos.GetHashCode());
            if (rngSeed == 0) rngSeed = 1;

            var job = new EvaluateCandidatesJob
            {
                warmStart = warmStart,
                candidates = candidates,
                costs = costs,
                costInput = costInput,
                initialState = initialState,
                cfg = cfg,
                dynamics = dynamics,
                lastControl = lastControl,
                noiseStd = noiseStd,
                rngSeed = rngSeed
            };

            job.Schedule(samples, 1).Complete();

            var bestCost = costs[0];
            var bestIndex = 0;
            for (var i = 1; i < samples; i++)
            {
                if (costs[i] < bestCost)
                {
                    bestCost = costs[i];
                    bestIndex = i;
                }
            }

            NativeArray<Control>.Copy(candidates, bestIndex * horizon, result, 0, horizon);
            for (var i = 0; i < horizon; i++)
                sequence[i] = result[i];

            return bestCost;
        }

        public CostInput BuildCostInput(float2 goalPos, float2 goalVel = default,
            float enemyYaw = float.NaN, float enemyYawRate = 0f, float projectileSpeed = 0f)
        {
            return new CostInput
            {
                goalPos = goalPos,
                goalVel = goalVel,
                obstacles = obstacles,
                obstacleCount = lastObstacleCount,
                enemyYaw = enemyYaw,
                enemyYawRate = enemyYawRate,
                projectileSpeed = projectileSpeed,
                enemyStates = enemyStates.IsCreated ? enemyStates : default,
                enemyStateCount = enemyStates.IsCreated ? enemyStates.Length : 0,
            };
        }

        private void ConvertObstacles(AI.Scanning.ObstacleScan scan, bool useObstacles, float shipMass)
        {
            lastObstacleCount = (scan.count > 0 && useObstacles) ? scan.count : 0;
            var invShipMass = shipMass > 0f ? 1f / shipMass : 1f;
            for (var i = 0; i < lastObstacleCount; i++)
            {
                var obs = scan.buffer[i];
                var rb = obs.collider ? obs.collider.attachedRigidbody : null;
                var obsMass = rb ? rb.mass : shipMass;
                obstacles[i] = new ObstacleData
                {
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
