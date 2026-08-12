#if UNITY_EDITOR
using AI;
using AI.Context;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.Common;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Preservation pins for the three-lane split: each production objective shape must land on the same Navigator control surface the retired ActIntent produced. The mapping table lives in PR-1's description; these are its executable half.</summary>
    [Category("MPC")]
    public class PilotDecisionSeamEditModeTests
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

        private const float ProjectileSpeed = 40f;

        private Navigator nav;
        private MpcSettings createdSettings;

        [SetUp]
        public void SetUp()
        {
            var host = new GameObject("SeamNav");
            var scout = host.AddComponent<Scout>();
            nav = host.AddComponent<Navigator>();
            nav.Initialize(new StubStatus(), default, scout, new SeedScope(1), ProjectileSpeed);
            createdSettings = nav.mpcSettings;
        }

        [TearDown]
        public void TearDown()
        {
            if (nav) Object.DestroyImmediate(nav.gameObject);
            if (createdSettings) Object.DestroyImmediate(createdSettings);
        }

        // The host resolves the anchor, so any valid id serves here.
        private static readonly ShipId AnchorId = new(1);

        // Enemy heading 90° so the MPC-convention conversion (fwd = (−sin, cos)) is observable.
        private static EnemyTarget Anchor() => new()
        {
            kinematics = new Kinematics(new Vector2(0f, 10f), new Vector2(1f, 2f), 90f, 30f, 0f),
        };

        [Test]
        public void DummyShape_PlanarZero_ArmsAStopWithNoAnchoredChannels()
        {
            nav.ApplyObjective(NavObjective.Planar(Vector2.zero), Anchor());

            Assert.That(nav.ShouldIdle(), Is.False, "a zero reference is a commanded stop, not idle");
            Assert.That((Vector2)nav.velocityReference, Is.EqualTo(Vector2.zero));
            Assert.That(nav.anchored.hasFacing, Is.False);
            Assert.That(nav.anchored.hasVelocity, Is.False);
            Assert.That(float.IsNaN(nav.enemyYaw), Is.True);
        }

        [Test]
        public void ArchetypeShape_PlanarPlusAimedFacing_KeepsTheWorldReferenceAndAnchorsTheNose()
        {
            var vRef = new Vector2(3f, -4f);
            nav.ApplyObjective(NavObjective.Anchored(AnchorId).Planar(vRef).Facing(0f, 1f), Anchor());

            Assert.That(nav.velocityReference.x, Is.EqualTo(3f), "exact-float: the world reference passes through");
            Assert.That(nav.velocityReference.y, Is.EqualTo(-4f));
            Assert.That(nav.anchored.hasFacing, Is.True);
            Assert.That(nav.anchored.facingOffsetRad, Is.EqualTo(0f));
            Assert.That(nav.anchored.facingWeight, Is.EqualTo(1f));
            Assert.That(nav.anchored.hasVelocity, Is.False, "the archetypes move in world frame, not enemy-polar");
        }

        [Test]
        public void PolicyShape_AnchoredVelocityAndFacing_LeavesTheWorldReferenceAtZero()
        {
            nav.ApplyObjective(NavObjective.Anchored(AnchorId)
                .Velocity(4f, -2f, 0.6f)
                .Facing(1.2f, 0.4f), Anchor());

            Assert.That((Vector2)nav.velocityReference, Is.EqualTo(Vector2.zero),
                "the polar channel carries the command; the world reference stays armed-but-unused");
            Assert.That(nav.ShouldIdle(), Is.False);
            Assert.That(nav.anchored.radialSpeed, Is.EqualTo(4f));
            Assert.That(nav.anchored.tangentialSpeed, Is.EqualTo(-2f));
            Assert.That(nav.anchored.velocityWeight, Is.EqualTo(0.6f));
            Assert.That(nav.anchored.facingOffsetRad, Is.EqualTo(1.2f));
            Assert.That(nav.anchored.facingWeight, Is.EqualTo(0.4f));
        }

        [Test]
        public void AnchoredChannels_ConvertTheEnemySnapshotToMpcConventionAtTheBoundary()
        {
            nav.ApplyObjective(NavObjective.Anchored(AnchorId).Velocity(1f, 0f, 1f), Anchor());

            Assert.That(nav.enemyPos.x, Is.EqualTo(0f));
            Assert.That(nav.enemyPos.y, Is.EqualTo(10f));
            Assert.That(nav.enemyVel.x, Is.EqualTo(1f));
            Assert.That(nav.enemyVel.y, Is.EqualTo(2f));
            // yaw 90° → fwd (−1, 0) → atan2(1, 0) = +π/2.
            Assert.That(nav.enemyYaw, Is.EqualTo(0.5f * Mathf.PI).Within(1e-5f));
            Assert.That(nav.enemyYawRate, Is.EqualTo(30f * Mathf.Deg2Rad).Within(1e-5f));
        }

        [Test]
        public void ProjectileSpeed_IsHostInjected_AndSurvivesAnAnchorlessObjective()
        {
            nav.ApplyObjective(NavObjective.Planar(new Vector2(1f, 0f)), Anchor());

            Assert.That(nav.projectileSpeed, Is.EqualTo(ProjectileSpeed),
                "our own ballistics are injected once at Initialize, not carried per decision");
        }

        [Test]
        public void FireControl_DefaultIsHold_AndTheThreeStatesAreDistinct()
        {
            Assert.That(default(FireControl).IsAuto, Is.False);
            Assert.That(default(FireControl).IsCommanded, Is.False);

            Assert.That(FireControl.Auto.IsAuto, Is.True);
            Assert.That(FireControl.Auto.IsCommanded, Is.False);

            Assert.That(FireControl.Commanded(true).IsCommanded, Is.True);
            Assert.That(FireControl.Commanded(true).Held, Is.True);
            Assert.That(FireControl.Commanded(false).Held, Is.False);
            Assert.That(FireControl.Commanded(false).IsCommanded, Is.True,
                "a released commanded trigger is still commanded — silence is Hold, not Commanded(false)");
        }

        [Test]
        public void BrainDecision_DefaultsBothSlotsToHold()
        {
            var decision = new BrainDecision(NavObjective.Planar(Vector2.zero));

            Assert.That(decision.primary.IsAuto, Is.False);
            Assert.That(decision.secondary.IsAuto, Is.False);
            Assert.That(decision.boost, Is.False);
        }
    }
}
#endif
