#if UNITY_EDITOR
using System.Collections.Generic;
using AI;
using AI.Context;
using Combat.Targeting;
using Movement;
using NUnit.Framework;
using Ships.Command;
using Ships.Registry;
using UnityEngine;

namespace Tests.EditMode.Tactics
{
    [Category("AI")]
    public class SecondsSinceHitEditModeTests
    {
        private sealed class StubStatus : IShipStatus
        {
            public float HealthPct { get; set; } = 1f;
            public float ShieldPct { get; set; } = 1f;
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => Dynamics.Default;
            public bool BoostAvailable => true;
            public float BoostCooldownRemaining => 0f;
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => 10f;
            public float MaxYawRate => 90f;
            public IReadOnlyList<ITrackingMissile> TrackingMissiles => System.Array.Empty<ITrackingMissile>();
        }

        private GameObject host;

        [SetUp]
        public void SetUp() => host = new GameObject("HitClock");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void ZeroesOnAShieldOrHullLoss_AndCountsUpOtherwise()
        {
            var status = new StubStatus();
            var context = new AIContext(status, host.AddComponent<Scout>(), holdRange: null);
            Assert.IsTrue(float.IsPositiveInfinity(context.SecondsSinceHit), "a ship never hit reads forever ago");

            status.ShieldPct = 0.5f;
            context.Update(0.1f);
            Assert.AreEqual(0f, context.SecondsSinceHit);
            status.ShieldPct = 0.6f;
            context.Update(0.1f);
            context.Update(0.1f);
            Assert.AreEqual(0.2f, context.SecondsSinceHit, 1e-5f, "regeneration is not a hit");
            status.HealthPct = 0.9f;
            context.Update(0.1f);
            Assert.AreEqual(0f, context.SecondsSinceHit);
        }
    }
}
#endif
