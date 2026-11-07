using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ships;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>The hunt's search clock with injected time, and the wave director holding its opening lull for it: with no patrol calling, the clock sounds the alarm itself, and phase one opens at the alarm keeping index 0, then the table runs as authored. The trial sectors wire a hunt of one patrol, then two, flying Crimson, and the HUD fixtures opt out.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorHuntEditModeTests
    {
        private const float SearchSeconds = 45f;
        private const float OpeningLull = 5f;
        private const float Lull = 12f;

        private readonly List<Object> created = new();
        private readonly List<(int sector, HuntAlarmReason reason, Ship caller)> alarms = new();
        private SectorHunt hunt;
        private WaveDirector director;

        private static SectorPhase[] Phases() => new[]
        {
            new SectorPhase { seconds = 40f, interval = 6f, cap = 2 },
            new SectorPhase { seconds = 30f, interval = 4f, cap = 3 },
        };

        [SetUp]
        public void SetUp()
        {
            var feel = Track(ScriptableObject.CreateInstance<HuntFeelProfile>());
            feel.searchSeconds = SearchSeconds;
            hunt = Track(new GameObject("Hunt")).AddComponent<SectorHunt>();
            hunt.Configure(feel, null, null, 0, 0f);
            var hero = Track(new GameObject("Hero")).AddComponent<Ship>();
            Drive(hunt.Build(new SectorBuildContext(null, null, true, null, default, hero: hero, sectorIndex: 2)));
            hunt.AlarmRaised += (sector, reason, caller) => alarms.Add((sector, reason, caller));

            director = Track(new GameObject("WaveDirector")).AddComponent<WaveDirector>();
            director.Configure(null, Phases(), Lull, openingLullSeconds: OpeningLull, hunt: hunt);
            director.BeginSchedule(0f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item) Object.DestroyImmediate(item);
            created.Clear();
            alarms.Clear();
        }

        [Test]
        public void SearchClock_SoundsTheAlarm_WithTheSectorIndexAndNoCaller()
        {
            Tick(0f);
            Tick(SearchSeconds - .1f);
            Assert.IsFalse(hunt.Alarmed);
            Assert.AreEqual(.1f, hunt.SearchSecondsLeft, 1e-3f);
            Assert.IsNull(hunt.CallInSecondsLeft, "With no patrol calling there is no call-in.");

            Tick(SearchSeconds);
            Assert.IsTrue(hunt.Alarmed);
            Assert.AreEqual(new[] { (2, HuntAlarmReason.SearchClock, (Ship)null) }, alarms);
            Tick(SearchSeconds + 1f);
            Assert.AreEqual(1, alarms.Count, "The alarm sounds once.");
        }

        [Test]
        public void Director_HoldsTheOpeningLullThroughTheHunt_ThenPhaseOneOpensAtTheAlarmAsIndexZero()
        {
            Tick(0f);
            Tick(30f);
            Assert.IsTrue(director.Phase.InOpeningLull, "The hunt outlasts the authored opening lull.");
            Assert.IsFalse(director.OpensPhase());

            Tick(SearchSeconds);
            Assert.IsFalse(director.Phase.InLull, "Phase one opens at the alarm.");
            Assert.AreEqual(0, director.Phase.Index, "The hunt adds no sector phase.");
            Assert.IsTrue(director.OpensPhase());

            Tick(SearchSeconds + 39f);
            Assert.AreEqual(0, director.Phase.Index, "Phase one runs its authored length from the alarm.");
            Tick(SearchSeconds + 41f);
            Assert.IsTrue(director.Phase.InLull);
            Assert.AreEqual(1, director.Phase.Index);
        }

        [Test]
        public void Director_FastForwardedPastTheOpening_IsNotPulledBack()
        {
            Tick(0f);
            director.BeginSchedule(-500f);
            director.Advance(0f);
            Tick(1f);
            Assert.IsTrue(director.Phase.Done, "A schedule already past its opening lull runs on.");
        }

        [TestCase("TrialSector", 1)]
        [TestCase("TrialSector2", 2)]
        [TestCase("TrialSector3", 2)]
        public void TrialSector_HoldsForItsHunt_OfItsPatrolCount_FlyingItsFactionsCommonShip(string sector, int patrols)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/{sector}.prefab");
            var hunt = prefab.Spawners.OfType<SectorHunt>().Single();
            using var serialized = new SerializedObject(hunt);
            Assert.AreEqual(patrols, serialized.FindProperty("patrolCount").intValue);
            Assert.AreSame(prefab.Module<SectorFaction>(), serialized.FindProperty("faction").objectReferenceValue,
                "Patrols fly the sector's faction.");
            Assert.IsTrue(serialized.FindProperty("patrolPilot").objectReferenceValue, "Patrols fly a wave pilot.");
            Assert.IsTrue(serialized.FindProperty("feel").objectReferenceValue);
            Assert.AreSame(hunt, Field(prefab.Spawners.OfType<WaveDirector>().Single(), "hunt"));
            Assert.AreSame(hunt, Field(prefab.GetComponent<PhaseSpineModule>(), "hunt"));
        }

        [TestCase("HudQuietSector")]
        [TestCase("HudExtractionSector")]
        public void HudFixtureSector_RunsNoHunt(string sector)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/Preview/{sector}.prefab");
            Assert.IsNull(Field(prefab.Spawners.OfType<WaveDirector>().Single(), "hunt"));
            Assert.IsNull(Field(prefab.GetComponent<PhaseSpineModule>(), "hunt"));
            Assert.IsFalse(prefab.GetComponentInChildren<SectorHunt>(true).gameObject.activeSelf);
        }

        private static Object Field(Object owner, string field)
        {
            using var serialized = new SerializedObject(owner);
            var value = serialized.FindProperty(field).objectReferenceValue;
            return value ? value : null;
        }

        private void Tick(float now)
        {
            hunt.Tick(now);
            director.HoldForHunt(now);
            director.Advance(now);
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }

        private static void Drive(IEnumerator routine)
        {
            while (routine.MoveNext()) { }
        }
    }
}
