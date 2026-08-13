#if UNITY_EDITOR
using System;
using AI;
using AI.Context;
using Game.RLHarness;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the collapsed archetype seam: the enum → law dispatch through Configure/Decide (both the episode roster and the authored pilot ride the same component), the Dummy's no-target constant, the unknown-enum failure, and the ArchetypePilot authoring that makes an archetype flyable in a live sector.</summary>
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

        private ArchetypeBrain NewBrain(OpponentArchetype archetype, Ship target)
        {
            var brain = brainHost.AddComponent<ArchetypeBrain>();
            var shape = Shape();
            brain.Configure(target, archetype, in shape, JukeSeed, Vector2.zero, borderRadius: 500f);
            return brain;
        }

        private AIContext Ctx(Kinematics self) =>
            new(new StubStatus { Kinematics = self }, brainHost.AddComponent<AI.Scout>());

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
    }
}
#endif
