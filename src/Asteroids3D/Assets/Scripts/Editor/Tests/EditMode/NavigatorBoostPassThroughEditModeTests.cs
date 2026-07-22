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
    /// <summary>Pins the boost command seam: an intent's boost ORs into the drive command over the solver's own boost, and resets never leak it. ApplyIntent/ApplyControl read only the control surface, so no solve runs.</summary>
    [Category("MPC")]
    public class NavigatorBoostPassThroughEditModeTests
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
            var host = new GameObject("NavigatorBoost");
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

        private static NavigationIntent VelocityIntent(bool boost) => new()
        {
            isValid = true,
            velocityReference = new Vector2(3f, 0f),
            boost = boost,
        };

        [Test]
        public void VelocityReference_BoostCommanded_OrsIntoCommand()
        {
            nav.ApplyIntent(VelocityIntent(boost: true));
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(1f, nav.CurrentCommand.boost);
        }

        [Test]
        public void VelocityReference_NoBoost_SolverBoostPassesThrough()
        {
            nav.ApplyIntent(VelocityIntent(boost: false));
            nav.ApplyControl(new MpcResult { boost = 1f });
            Assert.AreEqual(1f, nav.CurrentCommand.boost);
        }

        [Test]
        public void InvalidIntent_ClearsCommandedBoost()
        {
            nav.ApplyIntent(VelocityIntent(boost: true));
            nav.ApplyIntent(NavigationIntent.None);
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(0f, nav.CurrentCommand.boost);
        }

        [Test]
        public void NextIntentWithoutBoost_DropsThePriorCommand()
        {
            nav.ApplyIntent(VelocityIntent(boost: true));
            nav.ApplyIntent(VelocityIntent(boost: false));
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(0f, nav.CurrentCommand.boost);
        }
    }
}
#endif
