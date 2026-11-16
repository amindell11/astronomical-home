#if UNITY_EDITOR
using System.Collections.Generic;
using AI.Context;
using Combat.Targeting;
using Movement;
using NUnit.Framework;
using Ships.Command;
using Ships.Registry;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("AI")]
    public class LimpingShipEditModeTests
    {
        private const float Scale = 0.6f;

        private sealed class Status : IShipStatus
        {
            public Dynamics Dynamics { get; set; } = Dynamics.Default;
            public float HealthPct { get; set; } = 1f;
            public bool BoostAvailable { get; set; } = true;
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => default;
            public float ShieldPct => 1f;
            public float BoostCooldownRemaining => 0f;
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => Dynamics.maxSpeed;
            public float MaxYawRate => Dynamics.maxYawRate;
            public IReadOnlyList<ITrackingMissile> TrackingMissiles => System.Array.Empty<ITrackingMissile>();
        }

        [Test]
        public void AtOrAboveHull_PassesDynamicsMaxSpeedAndBoostThrough()
        {
            var inner = new Status { HealthPct = 0.5f, BoostAvailable = true };
            var atLevel = new LimpingShip(inner, limpBelowHull: 50f, limpScale: Scale);
            var above = new LimpingShip(inner, limpBelowHull: 40f, limpScale: Scale);

            Assert.IsFalse(atLevel.Limping, "at the hull level it is not below");
            Assert.AreEqual(inner.Dynamics, atLevel.Dynamics);
            Assert.AreEqual(inner.MaxSpeed, atLevel.MaxSpeed);
            Assert.IsTrue(atLevel.BoostAvailable);

            Assert.IsFalse(above.Limping);
            Assert.AreEqual(inner.Dynamics, above.Dynamics);
            Assert.AreEqual(inner.MaxSpeed, above.MaxSpeed);
            Assert.IsTrue(above.BoostAvailable);
        }

        [Test]
        public void BelowHull_ScalesTopSpeedAndForwardAcceleration_AndHasNoBoost()
        {
            var inner = new Status { HealthPct = 0.25f, BoostAvailable = true };
            var limping = new LimpingShip(inner, limpBelowHull: 40f, limpScale: Scale);

            Assert.IsTrue(limping.Limping);
            Assert.AreEqual(inner.Dynamics.Scaled(Scale, Scale), limping.Dynamics);
            Assert.AreEqual(inner.Dynamics.maxSpeed * Scale, limping.Dynamics.maxSpeed, 1e-5f);
            Assert.AreEqual(inner.Dynamics.forwardAcc * Scale, limping.Dynamics.forwardAcc, 1e-5f);
            Assert.AreEqual(inner.MaxSpeed * Scale, limping.MaxSpeed, 1e-5f);
            Assert.IsFalse(limping.BoostAvailable);
        }

        [Test]
        public void ZeroHullLevel_NeverLimps()
        {
            var inner = new Status { HealthPct = 0f, BoostAvailable = true };
            var limping = new LimpingShip(inner, limpBelowHull: 0f, limpScale: Scale);

            Assert.IsFalse(limping.Limping);
            Assert.AreEqual(inner.Dynamics, limping.Dynamics);
            Assert.AreEqual(inner.MaxSpeed, limping.MaxSpeed);
            Assert.IsTrue(limping.BoostAvailable);
        }
    }
}
#endif
