using AI.Observation;
using Ships.Command;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Flattens the decision-boundary state into the fixed 23-float sensor vector (self token 8, hasTarget 1, target token 9, envelope bits 2, ego-frame arena-center 2, primary-weapon readiness 1). Distances/positions normalize by arenaRadius, velocities by MaxSpeed; the token pieces come from <see cref="ObservationExtractor"/> so their semantics stay single-sourced.</summary>
    public static class AgentObservations
    {
        public const int Size = 23;

        public static void Fill(float[] buffer, IShipStatus self, in TargetView target,
            bool inMyEnvelope, bool inEnemyEnvelope, bool primaryWeaponReady,
            Vector2 arenaCenterPlane, float arenaRadius)
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

            buffer[i] = primaryWeaponReady ? 1f : 0f;
        }
    }
}
