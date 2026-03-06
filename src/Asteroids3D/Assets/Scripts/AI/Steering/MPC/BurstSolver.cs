using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Movement.MPC
{
    public struct ObstacleData
    {
        public float2 position;
        public float radius;
    }

    [BurstCompile]
    public struct EvaluateCandidatesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Control> warmStart;
        [NativeDisableParallelForRestriction]
        public NativeArray<Control> candidates;
        public NativeArray<float> costs;

        [ReadOnly] public NativeArray<ObstacleData> obstacles;
        public int obstacleCount;

        public State initialState;
        public float2 goalPos;
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

            costs[candidateIndex] = EvalTrajectory(offset, horizon);
        }

        private float EvalTrajectory(int offset, int horizon)
        {
            var totalCost = 0f;
            var pos = new float2(initialState.pos.x, initialState.pos.y);
            var vel = new float2(initialState.vel.x, initialState.vel.y);
            var yaw = initialState.yaw;
            var yawRate = initialState.yawRate;
            var prevU = lastControl;

            for (var i = 0; i < horizon; i++)
            {
                var u = candidates[offset + i];
                var isTerminal = i == horizon - 1;
                totalCost += EvalCost(pos, vel, yaw, yawRate, u, prevU, isTerminal);
                StepModel(ref pos, ref vel, ref yaw, ref yawRate, u);
                prevU = u;
            }

            return totalCost;
        }

        private float EvalCost(float2 pos, float2 vel, float yaw, float yawRate,
            Control u, Control prevU, bool isTerminal)
        {
            var toGoal = goalPos - pos;
            var distToGoalSq = math.lengthsq(toGoal);

            var wVel = cfg.wVel;
            var wYaw = cfg.wYaw;

            if (distToGoalSq < cfg.arrivalDistanceSq)
            {
                var distToGoal = math.sqrt(distToGoalSq);
                var t = 1f - (distToGoal / cfg.arrivalDistance);
                wVel = math.lerp(cfg.wVel, cfg.wVel * cfg.arrivalVelScale, t);
                wYaw = math.lerp(cfg.wYaw, cfg.wYaw * cfg.arrivalYawScale, t);
            }

            var posCost = math.lengthsq(pos - goalPos) * cfg.wPos;
            var velCost = math.lengthsq(vel) * wVel;
            var headingCost = HeadingCost(pos, yaw, goalPos) * wYaw;
            var facingCost = FacingCost(yaw, cfg.facingTarget) * cfg.wFacing;
            var yawRateCost = yawRate * yawRate * cfg.wYawRate;

            var obstacleCost = 0f;
            if (cfg.wObstacle > 0f && obstacleCount > 0)
                obstacleCost = ObstacleCost(pos) * cfg.wObstacle;

            var stateCost = posCost + velCost + headingCost + facingCost + yawRateCost + obstacleCost;

            var effortCost = (u.thrust * u.thrust + u.strafe * u.strafe + u.yawTorque * u.yawTorque) * cfg.wEffort;

            var duT = (u.thrust - prevU.thrust) * cfg.invDt;
            var duS = (u.strafe - prevU.strafe) * cfg.invDt;
            var duY = (u.yawTorque - prevU.yawTorque) * cfg.invDt;
            var smoothnessCost = (duT * duT) * cfg.wSmoothnessThrust +
                                 (duS * duS) * cfg.wSmoothnessStrafe +
                                 (duY * duY) * cfg.wSmoothnessYaw;

            var total = stateCost + effortCost + smoothnessCost;
            if (isTerminal)
                total += cfg.terminalMultiplier * stateCost;

            return total;
        }

        private static float HeadingCost(float2 pos, float yaw, float2 goal)
        {
            const float headingGateDistSq = 4f;
            const float headingGateDist = 2f;

            var toGoal = goal - pos;
            var distSq = math.lengthsq(toGoal);
            if (distSq < 1e-8f) return 0f;

            var goalYaw = math.atan2(-toGoal.x, toGoal.y);
            var angErr = WrapRadians(yaw - goalYaw);
            var cost = angErr * angErr;

            if (distSq >= headingGateDistSq) return cost;

            var normalizedDist = math.sqrt(distSq) / headingGateDist;
            return cost * Smoothstep01(normalizedDist);
        }

        private static float FacingCost(float yaw, float targetYaw)
        {
            if (math.isnan(targetYaw)) return 0f;
            var err = WrapRadians(yaw - targetYaw);
            return err * err;
        }

        private float ObstacleCost(float2 pos)
        {
            const float epsilon = 0.01f;
            var cost = 0f;
            for (var i = 0; i < obstacleCount; i++)
            {
                var obs = obstacles[i];
                var range = obs.radius + cfg.obstacleThreshold;
                var rangeSq = range * range;
                var distSq = math.lengthsq(pos - obs.position);

                if (distSq >= rangeSq) continue;

                var dist = math.sqrt(distSq);
                var norm = dist / range;
                cost += 1f / ((norm + epsilon) * (norm + epsilon));
            }
            return cost;
        }

        private void StepModel(ref float2 pos, ref float2 vel, ref float yaw, ref float yawRate, Control u)
        {
            var sinYaw = math.sin(yaw);
            var cosYaw = math.cos(yaw);
            var fwd = new float2(-sinYaw, cosYaw);
            var right = new float2(cosYaw, sinYaw);

            var accF = ((u.thrust >= 0 ? dynamics.forwardAcc : dynamics.reverseAcc) * u.thrust) / dynamics.mass;
            var speedPct = math.clamp(math.length(vel) / dynamics.maxSpeed, 0f, 1f);
            var strafeMag = math.lerp(dynamics.maxStrafeAcc, dynamics.minStrafeAcc, speedPct);
            var accS = (strafeMag * u.strafe) / dynamics.mass;
            var acc = fwd * accF + right * accS;

            var dragFactor = math.max(0f, 1f - dynamics.linearDrag * cfg.dt);
            var nextVel = vel * dragFactor + acc * cfg.dt;
            var maxSpeedSq = dynamics.maxSpeed * dynamics.maxSpeed;
            if (math.lengthsq(nextVel) > maxSpeedSq)
                nextVel = math.normalize(nextVel) * dynamics.maxSpeed;

            var torqueAlpha = dynamics.yawTorque * u.yawTorque;
            var angDragFactor = math.max(0f, 1f - dynamics.angularDrag * cfg.dt);
            var nextYawRate = yawRate * angDragFactor + torqueAlpha * cfg.dt;
            nextYawRate = math.clamp(nextYawRate, -dynamics.maxYawRate, dynamics.maxYawRate);

            pos = pos + nextVel * cfg.dt;
            vel = nextVel;
            yaw = WrapRadians(yaw + nextYawRate * cfg.dt);
            yawRate = nextYawRate;
        }

        private static float WrapRadians(float angle)
        {
            const float twoPi = 2f * math.PI;
            if (angle > math.PI) return angle - twoPi;
            if (angle < -math.PI) return angle + twoPi;
            return angle;
        }

        private static float Smoothstep01(float x)
        {
            x = math.clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static float NextGaussian(ref Unity.Mathematics.Random rng)
        {
            var u1 = 1f - rng.NextFloat();
            var u2 = 1f - rng.NextFloat();
            return math.sqrt(-2f * math.log(u1)) * math.sin(2f * math.PI * u2);
        }
    }

    public static class BurstSampler
    {
        public static float Solve(
            State initialState,
            NativeArray<Control> warmStart,
            NativeArray<Control> candidates,
            NativeArray<float> costs,
            float2 goalPos,
            NativeArray<ObstacleData> obstacles,
            int obstacleCount,
            Config cfg,
            Dynamics dynamics,
            int samples,
            float noiseStd,
            Control lastControl,
            NativeArray<Control> resultBuffer)
        {
            var horizon = cfg.horizon;
            var rngSeed = (uint)(Time.frameCount * 7919 + initialState.pos.GetHashCode());
            if (rngSeed == 0) rngSeed = 1;

            var job = new EvaluateCandidatesJob
            {
                warmStart = warmStart,
                candidates = candidates,
                costs = costs,
                obstacles = obstacles,
                obstacleCount = obstacleCount,
                initialState = initialState,
                goalPos = goalPos,
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

            var srcOffset = bestIndex * horizon;
            NativeArray<Control>.Copy(candidates, srcOffset, resultBuffer, 0, horizon);

            return bestCost;
        }
    }
}
