using Movement;
using Ships.Command;
using Ships.Registry;
using UnityEngine;

namespace Tests.Common
{
    public sealed class StubShipStatus : IShipStatus
    {
        public Transform transform;
        public Dynamics dynamics;
        public ShipId Id => default;
        public Transform Transform => transform;
        public Kinematics Kinematics => default;
        public Dynamics Dynamics => dynamics;
        public float HealthPct => 1f;
        public float ShieldPct => 1f;
        public bool BoostAvailable => true;
        public float BoostCooldownRemaining => 0f;
        public float BoostCooldownPct => 0f;
        public float MaxSpeed => dynamics.maxSpeed;
        public float MaxYawRate => dynamics.maxYawRate;
    }
}
