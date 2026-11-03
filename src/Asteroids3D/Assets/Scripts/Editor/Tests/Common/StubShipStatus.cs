using Movement;
using Ships.Command;
using Ships.Registry;
using UnityEngine;
using System.Collections.Generic;
using Combat.Targeting;

namespace Tests.Common
{
    public sealed class StubShipStatus : IShipStatus
    {
        public Transform transform;
        public Dynamics dynamics;
        public Kinematics kinematics;
        public ShipId Id => default;
        public Transform Transform => transform;
        public Kinematics Kinematics => kinematics;
        public Dynamics Dynamics => dynamics;
        public float HealthPct => 1f;
        public float ShieldPct => 1f;
        public bool BoostAvailable { get; set; } = true;
        public float BoostCooldownRemaining => 0f;
        public float BoostCooldownPct { get; set; }
        public float MaxSpeed => dynamics.maxSpeed;
        public float MaxYawRate => dynamics.maxYawRate;
        public IReadOnlyList<ITrackingMissile> TrackingMissiles => System.Array.Empty<ITrackingMissile>();
    }
}
