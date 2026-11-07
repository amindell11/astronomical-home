#if UNITY_EDITOR
using System;
using AI;
using AI.Context;
using Movement;
using AI.Navigation.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEditor;
using UnityEngine;
using Ships.Registry;
using RL.Opponents;
using System.Collections.Generic;
using Combat.Targeting;

namespace Tests.EditMode
{
    /// <summary>Pins the collapsed archetype seam: the enum → law dispatch through Configure/Decide (both the episode roster and the authored pilot ride the same component), the Dummy's no-target constant, the unknown-enum failure, the ArchetypePilot authoring that makes an archetype flyable in a live sector, the waypoint roam a live-targeting pilot flies once its target is gone, and the pure missile-evasion laws.</summary>
    [Category("AI")]
    public class ArchetypeBrainEditModeTests
    {
        private const string ArchetypePilotPath = "Assets/Prefabs/Pilots/ArchetypePilot.prefab";
        private const int JukeSeed = 7;

        private static OpponentDraw Shape() => new()
        {
            speedFraction = 0.8f,
            jukePeriod = 1f,
            orbitRadius = 16f,
            orbitDirection = 1,
            desiredRange = 12f,
        };

        private sealed class StubStatus : IShipStatus
        {
            public Kinematics Kinematics { get; set; }
            public ShipId Id => default;
            public Transform Transform => null;
            public Dynamics Dynamics => Movement.Dynamics.Default;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable => true;
            public float BoostCooldownRemaining => 0f;
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => 10f;
            public float MaxYawRate => 90f;
            public IReadOnlyList<ITrackingMissile> TrackingMissiles => System.Array.Empty<ITrackingMissile>();
        }

        private sealed class StubMissile : ITrackingMissile
        {
            public StubMissile(Vector2 pos, Vector2 vel) => Kinematics = new Kinematics(pos, vel, 0f, 0f, 0f);

            public Kinematics Kinematics { get; }
        }

        private GameObject brainHost;
        private GameObject targetHost;

        [SetUp]
        public void SetUp()
        {
            brainHost = new GameObject("ArchetypeHost");
            targetHost = new GameObject("ArchetypeTarget");
        }

        [TearDown]
        public void TearDown()
        {
            if (brainHost) UnityEngine.Object.DestroyImmediate(brainHost);
            if (targetHost) UnityEngine.Object.DestroyImmediate(targetHost);
        }

        private ArchetypeBrain NewBrain(OpponentArchetype archetype, Ship target, bool liveTargeting = false,
            GameObject host = null)
        {
            var brain = (host ? host : brainHost).AddComponent<ArchetypeBrain>();
            if (liveTargeting)
            {
                var serialized = new SerializedObject(brain);
                serialized.FindProperty("liveTargeting").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var shape = Shape();
            brain.Configure(target, archetype, in shape, JukeSeed, Vector2.zero, borderRadius: 500f);
            return brain;
        }

        private AIContext Ctx(Kinematics self, float? holdRange = null) =>
            new(new StubStatus { Kinematics = self }, brainHost.AddComponent<AI.Scout>(), holdRange);

        [TestCase(OpponentArchetype.Aggressor)]
        [TestCase(OpponentArchetype.Kiter)]
        [TestCase(OpponentArchetype.Evader)]
        [TestCase(OpponentArchetype.Orbiter)]
        public void Decide_RoutesTheArchetypeToItsLaw(OpponentArchetype archetype)
        {
            var target = targetHost.AddComponent<Ship>();
            var brain = NewBrain(archetype, target);
            // Self off-origin so the laws are distinguishable; target sits at the origin (default kinematics).
            var self = new Kinematics(new Vector2(3f, -4f), new Vector2(1f, 2f), 0f, 0f, 0f);
            var shape = Shape();
            var speed = shape.speedFraction * Dynamics.Default.maxSpeed;

            var decision = brain.Decide(Ctx(self));

            Vector2 expected = archetype switch
            {
                OpponentArchetype.Evader => ArchetypeLaws.FleeVelocity(self.pos, target.Kinematics.pos,
                    new System.Random(JukeSeed).Next(2) == 0 ? -1 : 1, speed),
                OpponentArchetype.Orbiter => ArchetypeLaws.OrbitVelocity(in self, target.Kinematics,
                    shape.orbitRadius, shape.orbitDirection, shape.speedFraction, Dynamics.Default.maxSpeed),
                _ => RangerBrain.HoldRangeVelocity(in self, target.Kinematics, shape.desiredRange, speed),
            };
            Assert.IsTrue(decision.HasValue);
            Assert.AreEqual(expected.x, decision.Value.nav.planarVelocity.x,
                "exact-float: the dispatch must hand each archetype its own law and params");
            Assert.AreEqual(expected.y, decision.Value.nav.planarVelocity.y);
            Assert.IsTrue(decision.Value.nav.TryGetAnchorId(out _));
            Assert.AreEqual(archetype != OpponentArchetype.Evader, decision.Value.engagePrimary,
                "only the Evader never fires");
        }

        [TestCase(OpponentArchetype.Aggressor, 18f)]
        [TestCase(OpponentArchetype.Kiter, 18f)]
        [TestCase(OpponentArchetype.Aggressor, null)]
        public void Decide_RangedArchetype_HoldsTheWeaponsDeclaredRange_ElseItsOwn(OpponentArchetype archetype, float? holdRange)
        {
            var target = targetHost.AddComponent<Ship>();
            var brain = NewBrain(archetype, target);
            var self = new Kinematics(new Vector2(3f, -4f), new Vector2(1f, 2f), 0f, 0f, 0f);
            var shape = Shape();

            var decision = brain.Decide(Ctx(self, holdRange));

            var expected = RangerBrain.HoldRangeVelocity(in self, target.Kinematics, holdRange ?? shape.desiredRange,
                shape.speedFraction * Dynamics.Default.maxSpeed);
            Assert.AreNotEqual(18f, shape.desiredRange, "the authored range must differ for the cases to discriminate");
            Assert.IsTrue(decision.HasValue);
            Assert.AreEqual(expected, decision.Value.nav.planarVelocity);
        }

        [Test]
        public void Decide_Dummy_HoldsZeroVelocityWithoutATarget()
        {
            var brain = NewBrain(OpponentArchetype.Dummy, target: null);

            var decision = brain.Decide(Ctx(default));

            Assert.IsTrue(decision.HasValue, "the Dummy needs no target");
            Assert.IsTrue(decision.Value.nav.hasPlanarVelocity);
            Assert.AreEqual(Vector2.zero, decision.Value.nav.planarVelocity);
            Assert.IsFalse(decision.Value.nav.TryGetAnchorId(out _), "the Dummy never packs against a target");
            Assert.IsFalse(decision.Value.engagePrimary);
        }

        // The test context never acquires an enemy, so Decide drops the Configured target.
        [Test]
        public void Decide_LiveTargeting_TargetGone_RoamVelocityIgnoresTheNose()
        {
            var brain = NewBrain(OpponentArchetype.Aggressor, targetHost.AddComponent<Ship>(), liveTargeting: true);
            targetHost.SetActive(false);
            var pos = new Vector2(3f, -4f);

            var noseUp = brain.Decide(Ctx(new Kinematics(pos, Vector2.zero, 0f, 0f, 0f)));
            var noseTurned = brain.Decide(Ctx(new Kinematics(pos, Vector2.zero, 135f, 0f, 0f)));

            Assert.IsTrue(noseUp.HasValue && noseTurned.HasValue, "a pilot whose target is gone keeps flying");
            Assert.AreEqual(noseUp.Value.nav.planarVelocity, noseTurned.Value.nav.planarVelocity,
                "a roam velocity that follows the nose turns with it and spirals");
            Assert.AreEqual(Shape().speedFraction * Dynamics.Default.maxSpeed,
                noseTurned.Value.nav.planarVelocity.magnitude, 1e-4f, "inside the border it roams at patrol speed");
            Assert.IsFalse(noseTurned.Value.nav.TryGetAnchorId(out _));
            Assert.IsFalse(noseTurned.Value.engagePrimary);
            Assert.IsFalse(noseTurned.Value.engageSecondary);
        }

        [TestCase(OpponentArchetype.Aggressor, true, 35f, 60f)]
        [TestCase(OpponentArchetype.Orbiter, true, 35f, 60f)]
        [TestCase(OpponentArchetype.Kiter, false, 0f, 120f)]
        public void Roam_AggressorAndOrbiterCircle_KiterWanders(OpponentArchetype archetype, bool circles,
            float minDistance, float maxDistance)
        {
            var brain = NewBrain(archetype, targetHost.AddComponent<Ship>(), liveTargeting: true);
            targetHost.SetActive(false);

            brain.Decide(Ctx(new Kinematics(new Vector2(3f, -4f), Vector2.zero, 0f, 0f, 0f)));

            Assert.AreEqual(circles, brain.RoamCircling);
            Assert.That(Vector2.Distance(brain.RoamWaypoint, brain.RoamCenter),
                Is.InRange(minDistance - 1e-3f, maxDistance + 1e-3f));
        }

        [Test]
        public void Roam_CirclesItsLaps_ThenWanders()
        {
            var brain = NewBrain(OpponentArchetype.Aggressor, targetHost.AddComponent<Ship>(), liveTargeting: true);
            targetHost.SetActive(false);
            brain.Decide(Ctx(new Kinematics(new Vector2(3f, -4f), Vector2.zero, 0f, 0f, 0f)));
            var center = brain.RoamCenter;

            var ringWaypoints = 0;
            while (brain.RoamCircling && ringWaypoints < 50)
            {
                ringWaypoints++;
                brain.Decide(Ctx(new Kinematics(brain.RoamWaypoint, Vector2.zero, 0f, 0f, 0f)));
            }

            const float twoLaps = 720f;
            Assert.That(ringWaypoints, Is.InRange(Mathf.CeilToInt(twoLaps / ArchetypeRoam.StepMaxDegrees),
                Mathf.CeilToInt(twoLaps / ArchetypeRoam.StepMinDegrees)), "two laps at the ring's angular steps");
            for (var i = 0; i < 10; i++)
            {
                Assert.IsFalse(brain.RoamCircling, "once wandering, the pilot never circles again");
                Assert.That(Vector2.Distance(brain.RoamWaypoint, center), Is.LessThanOrEqualTo(120f + 1e-3f),
                    "wander waypoints stay in the disc around the wreck");
                brain.Decide(Ctx(new Kinematics(brain.RoamWaypoint, Vector2.zero, 0f, 0f, 0f)));
            }
        }

        [Test]
        public void Roam_TwoPilots_PickDifferentWaypoints()
        {
            var otherHost = new GameObject("ArchetypeOtherHost");
            try
            {
                var target = targetHost.AddComponent<Ship>();
                var first = NewBrain(OpponentArchetype.Aggressor, target, liveTargeting: true);
                var second = NewBrain(OpponentArchetype.Aggressor, target, liveTargeting: true, otherHost);
                targetHost.SetActive(false);
                var self = new Kinematics(new Vector2(3f, -4f), Vector2.zero, 0f, 0f, 0f);

                first.Decide(Ctx(self));
                second.Decide(Ctx(self));

                Assert.AreNotEqual(first.RoamWaypoint, second.RoamWaypoint,
                    "pilots sharing an authored seed must not bunch onto the same points");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherHost);
            }
        }

        [Test]
        public void Decide_LiveTargeting_TargetConcealed_ReturnsNoDecision()
        {
            var target = targetHost.AddComponent<Ship>();
            var brain = NewBrain(OpponentArchetype.Aggressor, target, liveTargeting: true);
            target.Lock.Conceal();

            Assert.IsNull(brain.Decide(Ctx(default)),
                "the commander hunts a concealed target's last-seen point only on a null decision");
        }

        [Test]
        public void Decide_UnknownArchetype_Throws()
        {
            var brain = NewBrain((OpponentArchetype)99, targetHost.AddComponent<Ship>());

            Assert.Throws<ArgumentOutOfRangeException>(() => brain.Decide(Ctx(default)));
        }

        [Test]
        public void ArchetypePilot_AuthorsALiveTargetingArchetypeBrain()
        {
            var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(ArchetypePilotPath);
            Assert.IsNotNull(pilot, $"Missing prefab: {ArchetypePilotPath}");

            var brain = pilot.GetComponent<Brain>();
            Assert.IsInstanceOf<ArchetypeBrain>(brain);
            Assert.IsTrue(new SerializedObject(brain).FindProperty("liveTargeting").boolValue,
                "an authored pilot without the live bit idles: nothing pins its target");
        }

        [Test]
        public void ArchetypePilot_NavigatorAuthorsScriptedTrackerWeight()
        {
            var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(ArchetypePilotPath);
            Assert.IsNotNull(pilot, $"Missing prefab: {ArchetypePilotPath}");

            var settings = pilot.GetComponent<Navigator>().mpcSettings;
            Assert.IsNotNull(settings, "ArchetypePilot must author MPC settings");
            Assert.AreEqual(50f, settings.wVelTrack,
                "the scripted velocity laws need the roster's tracker weight — the script default is too loose to hold a reference");
        }

        [TestCase(5f, 10f / 15f, TestName = "TimeToImpact_SlowMissile_ClosesAtTheSpeedFloor")]
        [TestCase(20f, 10f / 20f, TestName = "TimeToImpact_FastMissile_ClosesAtItsSpeed")]
        public void TimeToImpact_FloorsTheClosingSpeed(float missileSpeed, float expected)
        {
            var self = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f);
            var missile = new Kinematics(new Vector2(0f, -10f), new Vector2(0f, missileSpeed), 0f, 0f, 0f);

            Assert.AreEqual(expected, ArchetypeLaws.TimeToImpact(in self, in missile, speedFloor: 15f), 1e-5f);
        }

        [TestCase(3f, 0f)]
        [TestCase(2f, 0f)]
        [TestCase(1.25f, 0.5f)]
        [TestCase(0.5f, 1f)]
        [TestCase(0.1f, 1f)]
        public void EvasionUrgency_RampsFromTheFarTimeToTheNearTime(float timeToImpact, float expected)
        {
            Assert.AreEqual(expected, ArchetypeLaws.EvasionUrgency(timeToImpact, farTime: 2f, nearTime: 0.5f), 1e-5f);
        }

        [TestCase(0f, 3f, 180f, 1f, TestName = "EscapeSide_Moving_BreaksToTheVelocitySideOverTheNose")]
        [TestCase(0f, 0f, 0f, 1f, TestName = "EscapeSide_AtRest_BreaksToTheNoseSide")]
        [TestCase(0f, 0f, 180f, -1f, TestName = "EscapeSide_AtRestNoseReversed_BreaksToTheOtherSide")]
        public void EscapeHeading_IsSquareToTheApproachOnTheChosenSide(float velX, float velY, float yaw,
            float expectedY)
        {
            var self = new Kinematics(Vector2.zero, new Vector2(velX, velY), yaw, 0f, 0f);
            var missilePos = new Vector2(10f, 0f);

            var side = ArchetypeLaws.EscapeSide(in self, missilePos);
            var heading = ArchetypeLaws.EscapeHeading(self.pos, missilePos, side);

            Assert.AreEqual(0f, heading.x, 1e-5f, "square to the missile's approach");
            Assert.AreEqual(expectedY, heading.y, 1e-5f);
        }

        [Test]
        public void MostUrgentTracker_PicksTheShortestFlooredTimeToImpact()
        {
            var self = new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f);
            var fastFar = new StubMissile(new Vector2(0f, -20f), new Vector2(0f, 40f));
            var slowNear = new StubMissile(new Vector2(6f, 0f), Vector2.zero);
            var cruising = new StubMissile(new Vector2(-30f, 0f), new Vector2(15f, 0f));

            var picked = ArchetypeLaws.MostUrgentTracker(in self, new ITrackingMissile[] { fastFar, slowNear, cruising },
                speedFloor: 15f, out var timeToImpact);

            Assert.AreSame(slowNear, picked, "a parked missile 6 u away closes at the speed floor: 0.4 s beats 0.5 s and 2 s");
            Assert.AreEqual(0.4f, timeToImpact, 1e-5f);
        }

        [TestCase(0f, 4f, 2f)]
        [TestCase(1f, 0f, 20f)]
        [TestCase(0.5f, 2f, 11f)]
        public void EvasionVelocity_BlendsTheLawTowardTheEscapeByUrgency(float urgency, float expectedX, float expectedY)
        {
            var velocity = ArchetypeLaws.EvasionVelocity(new Vector2(4f, 2f), Vector2.up, escapeSpeed: 20f, urgency);

            Assert.AreEqual(expectedX, velocity.x, 1e-5f);
            Assert.AreEqual(expectedY, velocity.y, 1e-5f);
        }
    }
}
#endif
