#if UNITY_EDITOR
using System.Collections;
using Game;
using Game.Bootstrap;
using Game.Encounters;
using Game.Sectors;
using NUnit.Framework;
using Objectives;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>Encounters through the arena chokepoints: the bundle dies with the sector subtree on session teardown, and authored plane constants land at the arena offset.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class ArenaEncounterPlacementPlayModeTests
    {
        private const string KeyEncounterPath = "Assets/Prefabs/Encounters/Key Encounter.prefab";
        private const string ExtractionEncounterPath = "Assets/Prefabs/Encounters/Extraction Encounter.prefab";
        private const string ConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";

        private GameObject root;
        private Sector template;
        private SessionHost host;
        private GameSession session;
        private bool savedAudioPause;
        private bool savedPresentation;
        private bool savedVfx;

        [SetUp]
        public void SetUp()
        {
            savedAudioPause = AudioListener.pause;
            savedPresentation = GameSettings.PresentationEnabled;
            savedVfx = GameSettings.VfxEnabled;
            AudioListener.pause = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            if (template) Object.DestroyImmediate(template.gameObject);
            foreach (var leftover in Object.FindObjectsByType<Encounter>(FindObjectsSortMode.None))
                Object.DestroyImmediate(leftover.gameObject);
            foreach (var leftover in Object.FindObjectsByType<KeyPickup>(FindObjectsSortMode.None))
                Object.DestroyImmediate(leftover.gameObject);
            foreach (var leftover in Object.FindObjectsByType<ExtractionZone>(FindObjectsSortMode.None))
                Object.DestroyImmediate(leftover.gameObject);

            AudioListener.pause = savedAudioPause;
            GameSettings.SetPresentationEnabled(savedPresentation);
            GameSettings.SetVfxEnabled(savedVfx);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TeardownSession_WithoutUnload_DestroysTheRunningEncounterBundle()
        {
            BuildRiglessSession(Vector2.zero, LoadEncounter<KeyPickupEncounter>(KeyEncounterPath));
            yield return host.ComposeSession(session);
            yield return host.LoadSector(session);

            Assert.Greater(CountAlive<Encounter>(), 0, "LoadSector must start the sector's encounter");
            Assert.Greater(CountAlive<KeyPickup>(), 0, "The key encounter must spawn its key pickup");

            // Session exit without UnloadSector first — the sector is destroyed with runTeardown:false.
            yield return host.TeardownSession(session);
            yield return null;

            Assert.AreEqual(0, CountAlive<Encounter>(), "TeardownSession leaked the running encounter");
            Assert.AreEqual(0, CountAlive<KeyPickup>(), "TeardownSession leaked the encounter's key pickup");
            Assert.AreEqual(0, CountAlive<ExtractionZone>(), "TeardownSession leaked an extraction zone");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator EncounterSpawns_LandAtTheArenaOffset_NotTheWorldOrigin()
        {
            var offset = new Vector2(1500f, -700f);
            BuildRiglessSession(offset,
                LoadEncounter<KeyPickupEncounter>(KeyEncounterPath),
                LoadEncounter<ExtractionEncounter>(ExtractionEncounterPath));
            yield return host.ComposeSession(session);
            yield return host.LoadSector(session);

            Assert.AreEqual(2, session.ActiveSector.Modules.Count);
            foreach (var module in session.ActiveSector.Modules)
            {
                var encounter = ((EncounterSequenceModule)module).Active;
                Assert.IsNotNull(encounter, "Each module must have started its encounter");
                var spawned = encounter.ObjectiveTarget;
                Assert.IsNotNull(spawned, $"{encounter.GetType().Name} must spawn its objective object");

                var plane = GamePlane.WorldPointToPlane(spawned.position);
                Assert.Less((plane - offset).magnitude, 200f,
                    $"{encounter.GetType().Name}'s spawn must land near the arena offset — a dropped Arena.Place lands it at the world origin");
                Assert.Greater(plane.magnitude, offset.magnitude * 0.5f,
                    $"{encounter.GetType().Name}'s spawn must not land near the world origin");
            }

            yield return host.UnloadSector(session);
            yield return host.TeardownSession(session);
        }

        private void BuildRiglessSession(Vector2 offset, params Encounter[] encounterTemplates)
        {
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(ConfigPath);
            Assert.IsNotNull(config, $"Sector config missing at {ConfigPath}");

            template = new GameObject("EncounterSectorTemplate").AddComponent<Sector>();
            var modules = new SectorModule[encounterTemplates.Length];
            for (var i = 0; i < encounterTemplates.Length; i++)
            {
                var moduleGo = new GameObject($"EncounterModule{i}");
                moduleGo.transform.SetParent(template.transform, false);
                var module = moduleGo.AddComponent<EncounterSequenceModule>();
                module.SetEncounters(new[] { encounterTemplates[i] });
                modules[i] = module;
            }
            template.SetManifest(null, null, modules);

            root = new GameObject("ArenaRoot");
            host = root.AddComponent<SessionHost>();
            session = new GameSession
            {
                Profile = new SessionProfile
                {
                    sectorEntry = new SectorEntry { prefab = template, config = config },
                    buildPlayer = false,
                    presentation = false,
                    vfx = false,
                    offset = offset
                }
            };
        }

        private static T LoadEncounter<T>(string path) where T : Encounter
        {
            var prefab = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(prefab, $"Encounter prefab missing at {path}");
            return prefab;
        }

        private static int CountAlive<T>() where T : Component =>
            Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
    }
}
#endif
