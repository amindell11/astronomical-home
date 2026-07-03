using Movement;
using UnityEngine;

namespace Ships
{
    /// <summary>
    /// The flattened, runtime-facing view of a ship's stats, produced at spawn by copying each field
    /// from its single owner (frame / engine / shield). This is a PLAIN C# type — not a
    /// ScriptableObject — and is what all runtime consumers (movement, damage, forces, dynamics) read.
    /// There is no combination math: disjoint ownership means the resolver just copies each field from
    /// the source that owns it.
    /// </summary>
    public sealed class ResolvedShipStats
    {
        // ── Frame-owned ──
        public float mass = 1000f;
        public float maxBankAngle = 45f;
        public float shipRadius = 1f;
        public float maxHealth = 100f;
        public int startingLives = 1;

        // ── Engine-owned ──
        public float maxSpeed = 25f;
        public float maxYawRate = 180f;
        public float forwardForce = 7000f;
        public float reverseForce = 3500f;
        public float yawTorque = 7000f;
        public float angularDrag = 1.7f;
        public float bankTorque = 5000f;
        public float bankDamping = 200f;
        public float minStrafeForce = 4000f;
        public float maxStrafeForce = 5000f;
        public float linearDrag = .5f;
        public float boostImpulse = 14000f;
        public float boostCooldown = 3f;

        // ── Shield-owned ──
        public float maxShield = 50f;
        public float shieldRegenDelay = 4f;
        public float shieldRegenRate = 10f;

        /// <summary>
        /// Build a resolved stat block by copying each field from its owner. Any null source leaves
        /// that owner's fields at their defaults.
        /// </summary>
        public static ResolvedShipStats Resolve(FrameSettings frame, EngineModule engine, ShieldModule shield)
        {
            var r = new ResolvedShipStats();

            if (frame)
            {
                r.mass = frame.mass;
                r.maxBankAngle = frame.maxBankAngle;
                r.shipRadius = frame.shipRadius;
                r.maxHealth = frame.maxHealth;
                r.startingLives = frame.startingLives;
            }

            if (engine)
            {
                r.maxSpeed = engine.maxSpeed;
                r.maxYawRate = engine.maxYawRate;
                r.forwardForce = engine.forwardForce;
                r.reverseForce = engine.reverseForce;
                r.yawTorque = engine.yawTorque;
                r.angularDrag = engine.angularDrag;
                r.bankTorque = engine.bankTorque;
                r.bankDamping = engine.bankDamping;
                r.minStrafeForce = engine.minStrafeForce;
                r.maxStrafeForce = engine.maxStrafeForce;
                r.linearDrag = engine.linearDrag;
                r.boostImpulse = engine.boostImpulse;
                r.boostCooldown = engine.boostCooldown;
            }

            if (shield)
            {
                r.maxShield = shield.maxShield;
                r.shieldRegenDelay = shield.shieldRegenDelay;
                r.shieldRegenRate = shield.shieldRegenRate;
            }

            return r;
        }

        public Dynamics Dynamics => BuildDynamics(0f);

        public Dynamics BuildDynamics(float yawInertia) => new Dynamics
        (
            mass : mass,
            forwardAcc : forwardForce,
            reverseAcc : reverseForce,
            maxStrafeAcc : maxStrafeForce,
            minStrafeAcc : minStrafeForce,
            maxSpeed : maxSpeed,
            maxYawRate : maxYawRate * Mathf.Deg2Rad,
            yawTorque : yawTorque,
            angularDrag : angularDrag,
            linearDrag: linearDrag,
            yawInertia: yawInertia > 0f ? yawInertia : mass,
            bankTorque: bankTorque,
            bankDamping: bankDamping,
            maxBankAngleRad: maxBankAngle * Mathf.Deg2Rad,
            boostImpulse: boostImpulse,
            boostCooldown: boostCooldown,
            shipRadius: shipRadius
        );
    }
}
