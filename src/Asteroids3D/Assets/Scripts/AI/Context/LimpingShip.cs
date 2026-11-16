using System.Collections.Generic;
using Combat.Targeting;
using Movement;
using Ships.Command;
using Ships.Registry;
using UnityEngine;

namespace AI.Context
{
    internal sealed class LimpingShip : IShipStatus
    {
        private readonly IShipStatus inner;
        private readonly float limpBelowHull;
        private readonly float limpScale;

        public LimpingShip(IShipStatus inner, float limpBelowHull, float limpScale)
        {
            this.inner = inner;
            this.limpBelowHull = limpBelowHull;
            this.limpScale = limpScale;
        }

        public bool Limping => inner.HealthPct * 100f < limpBelowHull;

        public ShipId Id => inner.Id;
        public Transform Transform => inner.Transform;
        public Kinematics Kinematics => inner.Kinematics;
        public Dynamics Dynamics => Limping ? inner.Dynamics.Scaled(limpScale, limpScale) : inner.Dynamics;
        public float HealthPct => inner.HealthPct;
        public float ShieldPct => inner.ShieldPct;
        public bool BoostAvailable => Limping ? false : inner.BoostAvailable;
        public float BoostCooldownRemaining => inner.BoostCooldownRemaining;
        public float BoostCooldownPct => inner.BoostCooldownPct;
        public float MaxSpeed => Limping ? inner.MaxSpeed * limpScale : inner.MaxSpeed;
        public float MaxYawRate => inner.MaxYawRate;
        public IReadOnlyList<ITrackingMissile> TrackingMissiles => inner.TrackingMissiles;
    }
}
