using System;
using UnityEngine;

namespace Ships.Loadout
{
    [Serializable]
    public sealed class ShipScale
    {
        public static readonly ShipScale One = new();

        [Tooltip("Multiplier on the ship's authored root scale. Collider bounds, and with them the AI clearance radius and the inertia, follow it.")]
        [SerializeField, Min(.01f)] private float size = 1f;
        [Tooltip("Factor on the chassis mass.")]
        [SerializeField, Min(.01f)] private float mass = 1f;
        [Tooltip("Factor on the engine's forward, reverse and strafe forces.")]
        [SerializeField, Min(.01f)] private float thrust = 1f;
        [Tooltip("Factor on the engine's yaw torque, bank torque and bank damping.")]
        [SerializeField, Min(.01f)] private float torque = 1f;
        [Tooltip("Factor on the hull's hit points.")]
        [SerializeField, Min(.01f)] private float hull = 1f;

        public float Size => size;
        public float Mass => mass;
        public float Thrust => thrust;
        public float Torque => torque;
        public float Hull => hull;

        public ShipScale() { }

        public ShipScale(float size, float mass, float thrust, float torque, float hull)
        {
            this.size = size;
            this.mass = mass;
            this.thrust = thrust;
            this.torque = torque;
            this.hull = hull;
        }

        public void Apply(ResolvedShipStats stats)
        {
            stats.mass *= mass;
            stats.forwardForce *= thrust;
            stats.reverseForce *= thrust;
            stats.minStrafeForce *= thrust;
            stats.maxStrafeForce *= thrust;
            stats.yawTorque *= torque;
            stats.bankTorque *= torque;
            stats.bankDamping *= torque;
            stats.maxHealth *= hull;
        }
    }
}
