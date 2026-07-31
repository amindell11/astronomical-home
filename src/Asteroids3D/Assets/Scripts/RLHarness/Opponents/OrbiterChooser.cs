using AI.Context;
using AI.States;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Circles the live target at a jittered radius, firing from inside the envelope.</summary>
    public class OrbiterChooser : OpponentArchetypeChooser
    {
        private const float RadialGain = 0.9f;
        // A tangential-only rotating command needs a standing radius error ∝ v²/r to supply
        // the centripetal demand through the P-term — feed it forward instead (Kff in seconds).
        private const float CentripetalKff = 2.5f;
        private const float MinCentripetalRange = 1f;

        private float orbitRadius;
        private int orbitDirection = 1;
        private float projectileSpeed;

        public void Configure(Ship target, float orbitRadius, int orbitDirection, float speedFraction,
            float projectileSpeed, Vector2 arenaCenter, float borderRadius)
        {
            this.orbitRadius = orbitRadius;
            this.orbitDirection = orbitDirection >= 0 ? 1 : -1;
            this.projectileSpeed = projectileSpeed;
            Bind(target, speedFraction, arenaCenter, borderRadius);
        }

        protected override NavigationIntent BuildIntent(AIContext ctx)
        {
            var self = ctx.Self.Kinematics;
            var enemy = target.Kinematics;
            var maxSpeed = ctx.Self.Dynamics.maxSpeed;

            var los = enemy.pos - self.pos;
            var r = los.magnitude;
            var losHat = r > 1e-4f ? los / r : Vector2.up;
            var tangent = orbitDirection * new Vector2(-losHat.y, losHat.x);
            var vTan = speedFraction * maxSpeed;
            var centripetal = CentripetalKff * vTan * vTan / Mathf.Max(r, MinCentripetalRange);
            var radial = (RadialGain * (orbitRadius - r) - centripetal) * -losHat;
            var vRef = Vector2.ClampMagnitude(vTan * tangent + radial, maxSpeed);

            return new NavigationIntent
            {
                isValid = true,
                velocityReference = Steered(self.pos, vRef),
                hasTarget = true,
                target = new EnemyTarget
                {
                    kinematics = enemy,
                    dynamics = target.Dynamics,
                    source = target.transform,
                },
                aimAtTarget = true,
                projectileSpeed = projectileSpeed,
                enableFiring = true,
            };
        }
    }
}
