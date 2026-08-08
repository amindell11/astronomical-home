#if UNITY_EDITOR
using AI;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.Common;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the boost command seam: a commanded boost ORs into the drive command over the solver's own boost, and resets never leak it. CommandBoost/ApplyControl read only the control surface, so no solve runs.</summary>
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
            public float BoostCooldownPct => 0f;
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
            nav.Initialize(new StubStatus(), default, scout, new SeedScope(1), primaryProjectileSpeed: 0f);
            createdSettings = nav.mpcSettings;
        }

        [TearDown]
        public void TearDown()
        {
            if (nav) Object.DestroyImmediate(nav.gameObject);
            if (createdSettings) Object.DestroyImmediate(createdSettings);
        }

        private void Drive(bool boost)
        {
            nav.ApplyObjective(NavObjective.Planar(new Vector2(3f, 0f)));
            nav.CommandBoost(boost);
        }

        [Test]
        public void VelocityReference_BoostCommanded_OrsIntoCommand()
        {
            Drive(boost: true);
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(1f, nav.CurrentCommand.boost);
        }

        [Test]
        public void VelocityReference_NoBoost_SolverBoostPassesThrough()
        {
            Drive(boost: false);
            nav.ApplyControl(new MpcResult { boost = 1f });
            Assert.AreEqual(1f, nav.CurrentCommand.boost);
        }

        [Test]
        public void ResetNavigation_ClearsCommandedBoost()
        {
            Drive(boost: true);
            nav.ResetNavigation();
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(0f, nav.CurrentCommand.boost);
        }

        [Test]
        public void NextDecisionWithoutBoost_DropsThePriorCommand()
        {
            Drive(boost: true);
            Drive(boost: false);
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(0f, nav.CurrentCommand.boost);
        }

        [Test]
        public void DriftObjective_ResetsToIdleAndClearsBoost()
        {
            Drive(boost: true);
            nav.ApplyObjective(NavObjective.Drift);
            nav.ApplyControl(new MpcResult { boost = 0f });
            Assert.AreEqual(0f, nav.CurrentCommand.boost);
            Assert.That(nav.ShouldIdle(), Is.True);
        }
    }
}
#endif
