using Movement;
using Ships.Mods;
using UnityEngine;

namespace Ships.Loadout
{
    /// <summary>
    /// The flattened, runtime-facing view of a ship's stats, produced at spawn by copying each field
    /// from its single owner (the ship's own chassis / engine / shield). This is a PLAIN C# type — not
    /// a ScriptableObject — and is what all runtime consumers (movement, damage, forces, dynamics) read.
    /// Disjoint ownership means the resolver just copies each field from the source that owns it; the
    /// one scaling is a run's ship mods, applied once on top of the copy.
    /// </summary>
    public sealed class ResolvedShipStats
    {
        // ── Chassis-owned (the Ship itself) ──
        public float mass = 800f;
        public float maxBankAngle = 35f;
        // NOT chassis-owned: derived by Ship from the collider bounds at spawn. Default until then.
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

        // ── Shield-owned ──
        public float maxShield = 50f;
        public float shieldRegenDelay = 4f;
        public float shieldRegenRate = 10f;

        /// <summary>
        /// Build a resolved stat block by copying each field from its owner. Any null source leaves
        /// that owner's fields at their defaults.
        /// </summary>
        public static ResolvedShipStats Resolve(Ship ship, EngineModule engine, ShieldModule shield)
        {
            var r = new ResolvedShipStats();

            if (ship)
            {
                r.mass = ship.mass;
                r.maxBankAngle = ship.maxBankAngle;
                r.maxHealth = ship.maxHealth;
                r.startingLives = ship.startingLives;
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
            }

            if (shield)
            {
                r.maxShield = shield.maxShield;
                r.shieldRegenDelay = shield.shieldRegenDelay;
                r.shieldRegenRate = shield.shieldRegenRate;
            }

            return r;
        }

        /// <summary>The same copy with a run's ship mods applied once per field group.</summary>
        public static ResolvedShipStats Resolve(Ship ship, EngineModule engine, ShieldModule shield, ShipMods mods)
        {
            var r = Resolve(ship, engine, shield);
            r.maxHealth = mods.Apply(ShipModField.Hull, r.maxHealth);
            r.maxSpeed = mods.Apply(ShipModField.Speed, r.maxSpeed);
            r.forwardForce = mods.Apply(ShipModField.Thrust, r.forwardForce);
            r.reverseForce = mods.Apply(ShipModField.Thrust, r.reverseForce);
            r.maxYawRate = mods.Apply(ShipModField.Handling, r.maxYawRate);
            r.yawTorque = mods.Apply(ShipModField.Handling, r.yawTorque);
            r.minStrafeForce = mods.Apply(ShipModField.Handling, r.minStrafeForce);
            r.maxStrafeForce = mods.Apply(ShipModField.Handling, r.maxStrafeForce);
            r.maxShield = mods.Apply(ShipModField.ShieldCapacity, r.maxShield);
            r.shieldRegenRate = mods.Apply(ShipModField.ShieldRegen, r.shieldRegenRate);
            r.shieldRegenDelay = mods.Apply(ShipModField.ShieldRecharge, r.shieldRegenDelay);
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
            shipRadius: shipRadius
        );
    }
}
