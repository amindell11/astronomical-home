#if UNITY_EDITOR
using AI;
using AI.States;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.Common;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Guards <see cref="Navigator.ShouldIdle"/>: the navigator idles iff no velocity reference is armed. A zero reference is a valid "stop" (the arm flag gates, not the value), and an invalid intent disarms back to idle.</summary>
    [Category("MPC")]
    public class NavigatorIdleGateEditModeTests
    {
        private sealed class StubStatus : IShipStatus
        {
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => default;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable => true;
            public float BoostCooldownRemaining => 0f;
            public float MaxSpeed => 10f;
            public float MaxYawRate => 90f;
        }

        private Navigator nav;
        private MpcSettings createdSettings;

        [SetUp]
        public void SetUp()
        {
            var host = new GameObject("NavigatorIdleGate");
            var scout = host.AddComponent<Scout>();
            nav = host.AddComponent<Navigator>();
            nav.Initialize(new StubStatus(), default, scout, new SeedScope(1));
            createdSettings = nav.mpcSettings;
        }

        [TearDown]
        public void TearDown()
        {
            if (nav) Object.DestroyImmediate(nav.gameObject);
            if (createdSettings) Object.DestroyImmediate(createdSettings);
        }

        private static NavigationIntent VelocityIntent(Vector2 reference) => new()
        {
            isValid = true,
            velocityReference = reference,
        };

        [Test]
        public void FreshNavigator_Idles()
        {
            Assert.That(nav.ShouldIdle(), Is.True,
                "An unarmed navigator has nothing to act on.");
        }

        [Test]
        public void VelocityReference_Armed_DoesNotIdle()
        {
            nav.SetVelocityReference(new Vector2(5f, 0f));
            Assert.That(nav.ShouldIdle(), Is.False,
                "An armed velocity reference must keep the MPC running.");
        }

        [Test]
        public void ZeroReference_IsAValidStop_DoesNotIdle()
        {
            nav.SetVelocityReference(Vector2.zero);
            Assert.That(nav.ShouldIdle(), Is.False,
                "A zero reference commands 'stop' — the MPC must keep running to brake and hold.");
        }

        [Test]
        public void ApplyIntent_Valid_ArmsTheTracker()
        {
            nav.ApplyIntent(VelocityIntent(new Vector2(3f, 0f)));
            Assert.That(nav.ShouldIdle(), Is.False,
                "A valid intent is a velocity-reference command and must arm the navigator.");
        }

        [Test]
        public void ApplyIntent_Invalid_DisarmsToIdle()
        {
            nav.ApplyIntent(VelocityIntent(new Vector2(3f, 0f)));
            nav.ApplyIntent(NavigationIntent.None);
            Assert.That(nav.ShouldIdle(), Is.True,
                "An invalid intent must disarm the velocity reference back to idle.");
        }
    }
}
#endif
