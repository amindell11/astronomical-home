using System;
using AI.Observation;
using AI.Scanning;
using Ships.Command;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Flattens the decision-boundary state into the fixed 72-float sensor vector (self token 8, hasTarget 1, target token 9, envelope bits 2, ego-frame arena-center 2, primary-weapon readiness 1, self primary heat 1, then 8 nearest-asteroid tokens × 6). Distances/positions normalize by arenaRadius, velocities by MaxSpeed; the token pieces come from <see cref="ObservationExtractor"/> so their semantics stay single-sourced.</summary>
    public static class AgentObservations
    {
        public const int ObstacleTokenCount = 8;
        public const int ObstacleTokenFloats = 6;
        public const int Size = 24 + ObstacleTokenCount * ObstacleTokenFloats;

        // SpawnSettings.asset ceiling: largest mesh volume 121.41 at massScale 2.5 → radius ≈ 4.17.
        public const float SpawnSettingsMaxAsteroidRadius = 4.17f;

        public static void Fill(float[] buffer, IShipStatus self, in TargetView target,
            bool inMyEnvelope, bool inEnemyEnvelope, bool primaryWeaponReady, float primaryHeatPct,
            Vector2 arenaCenterPlane, float arenaRadius, in ObstacleScan asteroids)
        {
            var kin = self.Kinematics;
            var frame = new EgoFrame(kin.pos, kin.Forward);
            var maxSpeed = Mathf.Max(self.MaxSpeed, 1e-3f);
            var radius = Mathf.Max(arenaRadius, 1e-3f);
            var i = 0;

            var selfToken = ObservationExtractor.BuildSelf(self, frame);
            buffer[i++] = selfToken.velocity.x / maxSpeed;
            buffer[i++] = selfToken.velocity.y / maxSpeed;
            buffer[i++] = selfToken.speedPct;
            buffer[i++] = selfToken.yawRatePct;
            buffer[i++] = selfToken.healthPct;
            buffer[i++] = selfToken.shieldPct;
            buffer[i++] = selfToken.boostAvailable;
            buffer[i++] = selfToken.boostCooldownPct;

            buffer[i++] = target.has ? 1f : 0f;
            if (target.has)
            {
                var targetToken = ObservationExtractor.BuildTarget(frame, kin.vel, in target);
                buffer[i++] = targetToken.relPosition.x / radius;
                buffer[i++] = targetToken.relPosition.y / radius;
                buffer[i++] = targetToken.distance / radius;
                buffer[i++] = targetToken.relVelocity.x / maxSpeed;
                buffer[i++] = targetToken.relVelocity.y / maxSpeed;
                buffer[i++] = targetToken.facing.x;
                buffer[i++] = targetToken.facing.y;
                buffer[i++] = targetToken.healthPct;
                buffer[i++] = targetToken.shieldPct;
            }
            else
            {
                for (var z = 0; z < 9; z++) buffer[i++] = 0f;
            }

            buffer[i++] = inMyEnvelope ? 1f : 0f;
            buffer[i++] = inEnemyEnvelope ? 1f : 0f;

            var centerEgo = frame.Point(arenaCenterPlane) / radius;
            buffer[i++] = centerEgo.x;
            buffer[i++] = centerEgo.y;

            buffer[i++] = primaryWeaponReady ? 1f : 0f;
            buffer[i++] = primaryHeatPct;

            FillObstacleTokens(buffer, i, in frame, kin.pos, kin.vel, maxSpeed, radius, in asteroids);
        }

        /// <summary>Appends the k-nearest-asteroid token block: per slot relPos.xy, distance, relVel.xy, radius — ego frame, ascending by distance (the scan is unordered), zero-padded tail (radius 0 ⇔ empty slot).</summary>
        private static void FillObstacleTokens(float[] buffer, int i, in EgoFrame frame,
            Vector2 selfPos, Vector2 selfVel, float maxSpeed, float arenaRadius, in ObstacleScan asteroids)
        {
            var scan = asteroids.buffer;
            var count = scan == null ? 0 : Mathf.Min(asteroids.count, scan.Length);
            Span<int> order = stackalloc int[64];
            Span<float> distance = stackalloc float[64];
            if (count > order.Length)
            {
                order = new int[count];
                distance = new float[count];
            }
            for (var n = 0; n < count; n++)
            {
                order[n] = n;
                distance[n] = (scan[n].position - selfPos).magnitude;
            }

            var slots = Mathf.Min(ObstacleTokenCount, count);
            for (var s = 0; s < slots; s++)
            {
                var best = s;
                for (var n = s + 1; n < count; n++)
                    if (distance[order[n]] < distance[order[best]])
                        best = n;
                (order[s], order[best]) = (order[best], order[s]);

                var o = scan[order[s]];
                var relPos = frame.Point(o.position);
                var relVel = frame.Direction(o.velocity - selfVel);
                buffer[i++] = relPos.x / arenaRadius;
                buffer[i++] = relPos.y / arenaRadius;
                buffer[i++] = distance[order[s]] / arenaRadius;
                buffer[i++] = relVel.x / maxSpeed;
                buffer[i++] = relVel.y / maxSpeed;
                buffer[i++] = o.radius / SpawnSettingsMaxAsteroidRadius;
            }

            for (var s = slots; s < ObstacleTokenCount; s++)
                for (var z = 0; z < ObstacleTokenFloats; z++)
                    buffer[i++] = 0f;
        }
    }
}
