using System;
using AI;
using AI.Observation;
using AI.Scanning;
using Ships.Command;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Flattens the decision-boundary state into the fixed 28-float combat vector (self token 8, hasTarget 1, target token 9, envelope bits 2, ego-frame arena-center 2, self primary-weapon readiness 1, self primary heat 1, ego-frame intercept-lead direction 2, enemy primary-weapon readiness 1, enemy primary heat 1). Asteroids ride a separate variable-length attention buffer via <see cref="BuildObstacleTokens"/>. Distances/positions normalize by arenaRadius, velocities by MaxSpeed; the token pieces come from <see cref="ObservationExtractor"/> and the lead from <see cref="Gunner.AimPoint"/> so their semantics stay single-sourced.</summary>
    public static class AgentObservations
    {
        public const int CombatChannels = 28;
        public const int ObstacleTokenFloats = 7;
        // BufferSensor capacity, baked into the ONNX at export. Sized to cover the obstacle scan-box occupancy
        // at max training density (2.5): true P95 ≈ 108, but Scout.ObstacleScanner delivers at most 64 tokens
        // (its buffer ceiling), so 64 covers everything the obs pipeline can present. See the occupancy probe (PR body).
        public const int ObstacleTokenCap = 64;

        // Also baked into the ONNX at export: a host that names the buffer differently gets a rejected model,
        // so training and gameplay read it from here rather than each spelling it out.
        public const string ObstacleSensorName = "AsteroidBuffer";

        // SpawnSettings.asset ceiling: largest mesh volume 121.41 at massScale 2.5 → radius ≈ 4.17.
        public const float SpawnSettingsMaxAsteroidRadius = 4.17f;

        /// <summary>Sets the schema-shape bits both compose sites share (obs vector size, hybrid ActionSpec, obstacle attention-buffer dims). Behavior name/type/model stay per-site.</summary>
        public static void ApplySchema(BehaviorParameters behavior, BufferSensorComponent obstacleBuffer)
        {
            behavior.BrainParameters.VectorObservationSize = CombatChannels;
            behavior.BrainParameters.ActionSpec = new ActionSpec(
                AgentActions.Count, new[] { AgentActions.ChoicesPerBranch, AgentActions.ChoicesPerBranch });

            obstacleBuffer.SensorName = ObstacleSensorName;
            obstacleBuffer.ObservableSize = ObstacleTokenFloats;
            obstacleBuffer.MaxNumObservables = ObstacleTokenCap;
        }

        public static void Fill(float[] buffer, IShipStatus self, in TargetView target,
            bool inMyEnvelope, bool inEnemyEnvelope, bool primaryWeaponReady, float primaryHeatPct,
            float primaryProjectileSpeed, Vector2 arenaCenterPlane, float arenaRadius,
            bool enemyWeaponReady, float enemyHeatPct)
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

            // Manual aim's target picture: the unit ego direction toward the primary weapon's intercept point (the same lead truth the gunsight/envelope evaluate at).
            var lead = Vector2.zero;
            if (target.has)
            {
                var leadEgo = frame.Point(Gunner.AimPoint(in kin, target.pos, target.vel, primaryProjectileSpeed));
                if (leadEgo.sqrMagnitude > 1e-8f) lead = leadEgo.normalized;
            }
            buffer[i++] = lead.x;
            buffer[i++] = lead.y;

            // Enemy weapon state, target-conditional: heat-lasers with full lockout make ready non-derivable from heatPct, so both channels ride.
            buffer[i++] = target.has && enemyWeaponReady ? 1f : 0f;
            buffer[i++] = target.has ? enemyHeatPct : 0f;
        }

        /// <summary>Selects the nearest <paramref name="maxTokens"/> asteroids and writes their 7-float tokens (ego relPos.xy, distance, ego relVel.xy, radius, healthPct — normalized) contiguously into <paramref name="dest"/>, returning the token count. No ordering guarantee beyond nearest-N selection and no zero-pad: the attention buffer is variable-length, so absence is the mask, not a sentinel.</summary>
        public static int BuildObstacleTokens(float[] dest, int maxTokens, IShipStatus self,
            float arenaRadius, in ObstacleScan asteroids)
        {
            var kin = self.Kinematics;
            var frame = new EgoFrame(kin.pos, kin.Forward);
            var maxSpeed = Mathf.Max(self.MaxSpeed, 1e-3f);
            var radius = Mathf.Max(arenaRadius, 1e-3f);

            var scan = asteroids.buffer;
            var count = scan == null ? 0 : Mathf.Min(asteroids.count, scan.Length);
            var emit = Mathf.Min(maxTokens, count);

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
                distance[n] = (scan[n].position - kin.pos).magnitude;
            }

            var w = 0;
            for (var s = 0; s < emit; s++)
            {
                // Partial selection sort to emit slots: nearest-N without fully ordering the rest.
                var best = s;
                for (var n = s + 1; n < count; n++)
                    if (distance[order[n]] < distance[order[best]])
                        best = n;
                (order[s], order[best]) = (order[best], order[s]);

                var o = scan[order[s]];
                var relPos = frame.Point(o.position);
                var relVel = frame.Direction(o.velocity - kin.vel);
                dest[w++] = relPos.x / radius;
                dest[w++] = relPos.y / radius;
                dest[w++] = distance[order[s]] / radius;
                dest[w++] = relVel.x / maxSpeed;
                dest[w++] = relVel.y / maxSpeed;
                dest[w++] = o.radius / SpawnSettingsMaxAsteroidRadius;
                dest[w++] = o.healthPct;
            }
            return emit;
        }
    }
}
