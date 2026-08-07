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
