#if UNITY_EDITOR
using System;
using AI;
using Game.RLHarness;
using Movement.MPC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the live-archetype seam: the shared factory's archetype → chooser mapping (both the episode roster and the authored pilot route through it), and the ArchetypePilot authoring that makes an archetype flyable in a live sector.</summary>
    [Category("AI")]
    public class LiveArchetypeEditModeTests
    {
        private const string ArchetypePilotPath = "Assets/Prefabs/Pilots/ArchetypePilot.prefab";

        private static OpponentDraw Shape() => new()
        {
            speedFraction = 0.8f,
            jukePeriod = 1f,
            orbitRadius = 16f,
            orbitDirection = 1,
            desiredRange = 12f,
        };

        [TestCase(OpponentArchetype.Aggressor, typeof(HoldRangeFireChooser))]
        [TestCase(OpponentArchetype.Kiter, typeof(HoldRangeFireChooser))]
        [TestCase(OpponentArchetype.Evader, typeof(EvaderChooser))]
        [TestCase(OpponentArchetype.Orbiter, typeof(OrbiterChooser))]
        [TestCase(OpponentArchetype.Dummy, typeof(DummyChooser))]
        public void Create_MapsArchetypeToItsChooser(OpponentArchetype archetype, Type expected)
        {
            var shape = Shape();
            Assert.IsInstanceOf(expected,
                ArchetypeChoosers.Create(archetype, in shape, null, 30f, 1, Vector2.zero, 500f));
        }

        [TestCase(OpponentArchetype.Aggressor)]
        [TestCase(OpponentArchetype.Kiter)]
        [TestCase(OpponentArchetype.Evader)]
        [TestCase(OpponentArchetype.Orbiter)]
        public void Create_DefaultsToTheRosterCadence(OpponentArchetype archetype)
        {
            var shape = Shape();
            var chooser = (OpponentArchetypeChooser)ArchetypeChoosers.Create(
                archetype, in shape, null, 30f, 1, Vector2.zero, 500f);
            Assert.AreEqual(10, chooser.RecomputeIntervalTicks,
                "the roster's 5 Hz cadence is what every trained checkpoint and eval yardstick was measured against");
        }

        [Test]
        public void Create_HonorsAnExplicitCadence()
        {
            var shape = Shape();
            var chooser = (OpponentArchetypeChooser)ArchetypeChoosers.Create(
                OpponentArchetype.Aggressor, in shape, null, 30f, 1, Vector2.zero, 500f,
                ArchetypeDrive.Production, 1);
            Assert.AreEqual(1, chooser.RecomputeIntervalTicks);
        }

        [Test]
        public void ArchetypePilot_AuthorsEveryTickCadence()
        {
            var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(ArchetypePilotPath);
            Assert.IsNotNull(pilot, $"Missing prefab: {ArchetypePilotPath}");

            var interval = new SerializedObject(pilot.GetComponent<Brain>())
                .FindProperty("chooser").FindPropertyRelative("recomputeIntervalTicks");
            Assert.IsNotNull(interval, "LiveArchetypeChooser must author a recompute interval");
            Assert.AreEqual(1, interval.intValue, "the live pilot decides every sim tick (50 Hz)");
        }

        [Test]
        public void Create_UnknownArchetype_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var shape = Shape();
                ArchetypeChoosers.Create((OpponentArchetype)99, in shape, null, 30f, 1, Vector2.zero, 500f);
            });
        }

        [Test]
        public void ArchetypePilot_AuthorsLiveArchetypeChooser()
        {
            var pilot = AssetDatabase.LoadAssetAtPath<GameObject>(ArchetypePilotPath);
            Assert.IsNotNull(pilot, $"Missing prefab: {ArchetypePilotPath}");
            Assert.IsInstanceOf<LiveArchetypeChooser>(pilot.GetComponent<Brain>().Chooser);
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
