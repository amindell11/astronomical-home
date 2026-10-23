#if UNITY_EDITOR && ASTRO_FMOD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cameras;
using FMOD.Studio;
using FMODUnity;
using Game;
using Game.Audio;
using Game.Runs;
using NUnit.Framework;
using Objectives;
using Substrate.Sectors;
using Substrate.Sectors.Activation;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Audio
{
    [Category("Audio")]
    public class GameMusicPlayModeTests : FmodEvidenceFixture
    {
        private const string MusicPrefabPath = "Assets/Prefabs/Audio/GameMusic.prefab";
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";
        private const string MusicEvent = "event:/Music/Game";
        private const string MusicStateName = "MusicState";
        private const string PhaseName = "Phase";
        private const float StepTimeoutSec = 30f;

        private readonly List<GameObject> created = new();
        private GameObject hostGo;
        private GameHost host;
        private string recordPath;

        public override void TearDown()
        {
            if (hostGo) hostGo.GetComponent<UnitService>().Clear();
            DestroyTestObject(hostGo);
            foreach (var go in created) DestroyTestObject(go);
            created.Clear();
            if (File.Exists(recordPath)) File.Delete(recordPath);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator EachState_LandsOnTheStudioLabelOfItsName()
        {
            var description = Fresh(MusicEvent);
            var music = NewMusic(New("Objectives").AddComponent<ObjectiveService>());
            yield return Flush();

            var instances = Instances(description);
            Debug.Log($"[FMOD] {MusicEvent} instances={instances.Length}");
            Assert.AreEqual(1, instances.Length, "one music instance from Initialize");
            AssertPlaying(instances[0], "the music plays from Initialize");

            foreach (GameMusic.State state in Enum.GetValues(typeof(GameMusic.State)))
            {
                music.Set(state);
                yield return Flush();
                var label = Label(description, instances[0]);
                Debug.Log($"[FMOD] Set({state}): MusicState={Parameter(instances[0], MusicStateName)} ({label})");
                Assert.AreEqual(state.ToString(), label, $"{state} lands on its own label");
            }
        }

        [UnityTest]
        public IEnumerator PhasedSector_FeedsPhase_AndTheExtractStepSetsExtract()
        {
            var description = Fresh(MusicEvent);
            var objectives = New("Objectives").AddComponent<ObjectiveService>();
            var director = New("WaveDirector").AddComponent<WaveDirector>();
            director.Configure(null, new[]
            {
                new SectorPhase { seconds = 40f, interval = 6f, cap = 2 },
                new SectorPhase { seconds = 40f, interval = 6f, cap = 2 },
                new SectorPhase { seconds = 40f, interval = 6f, cap = 2 },
            }, lullSeconds: 5f);
            director.BeginSchedule(0f);
            var zone = New("Zone");
            zone.AddComponent<SphereCollider>().isTrigger = true;
            var spine = New("Spine").AddComponent<PhaseSpineModule>();
            spine.Bind(director, zone.AddComponent<ExtractionZone>());
            var sectorGo = New("Sector");
            sectorGo.SetActive(false);
            var sector = sectorGo.AddComponent<Sector>();
            sector.SetManifest(null, null, new SectorModule[] { spine });
            var ctx = new SectorBuildContext(null, objectives, true, sector, default, bus: new SectorEventBus());

            var music = NewMusic(objectives);
            yield return spine.Setup(ctx);
            music.EnterSector(sector);
            yield return null;
            yield return Flush();
            var instance = Instances(description)[0];
            Assert.AreEqual(nameof(GameMusic.State.Combat), Label(description, instance), "entering a sector");
            Assert.AreEqual(0f, Parameter(instance, PhaseName), "the first survive phase");

            director.Advance(50f);
            yield return null;
            yield return Flush();
            Assert.AreEqual(1f, Parameter(instance, PhaseName), "the second survive phase, past the lull");

            director.Advance(200f);
            yield return null;
            yield return Flush();
            Debug.Log($"[FMOD] spine step={objectives.SpineStep}: {Label(description, instance)}, " +
                      $"Phase={Parameter(instance, PhaseName)}");
            Assert.AreEqual(PhaseSpineModule.StepExtract, objectives.SpineStep);
            Assert.AreEqual(nameof(GameMusic.State.Extract), Label(description, instance), "the spine stepped to extract");
            Assert.AreEqual(2f, Parameter(instance, PhaseName), "the last phase holds through extraction");
            Assert.AreEqual(1, Instances(description).Length, "still the one instance");

            yield return spine.Teardown(ctx);
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator PresentingHost_WalksOneInstanceThroughTheRunSteps()
        {
            var description = Fresh(MusicEvent);
            var seen = new List<string>();
            var instanceCounts = new HashSet<int>();
            PlayerRig rig = null;
            Boot(presentation: true);

            yield return Until(() =>
            {
                rig = hostGo.GetComponentInChildren<PlayerRig>();
                return rig && rig.Tally.SecondsSurvived > 0f && InSector("SectorA");
            }, "the first sector starts", description, seen, instanceCounts);

            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return Until(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "the sector recap shows",
                description, seen, instanceCounts);
            Dismiss();
            yield return Until(() => Loaded("SectorB"), "the second sector loads", description, seen, instanceCounts);
            yield return Until(() => true, "the second sector's first frame", description, seen, instanceCounts);

            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return Until(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "the victory recap shows",
                description, seen, instanceCounts);
            Dismiss();
            yield return Until(() => Loaded("SectorA"), "the next run's first sector loads", description, seen,
                instanceCounts);
            yield return Until(() => true, "the next run's first frame", description, seen, instanceCounts);

            TestDamage.Kill(rig.Player);
            yield return Until(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "the defeat recap shows",
                description, seen, instanceCounts);

            Debug.Log($"[FMOD] run steps: {string.Join(" > ", seen)}; instance counts seen: {string.Join(",", instanceCounts)}");
            Assert.That(instanceCounts, Is.EquivalentTo(new[] { 1 }), "one music instance for the whole session");
            Assert.AreEqual(new[]
            {
                "Hangar", "Combat", "Recap", "Combat", "Victory", "Hangar", "Combat", "Defeat"
            }, seen);
        }

        [UnityTest]
        public IEnumerator HostWithPresentationOff_BuildsNoMusic()
        {
            var description = Fresh(MusicEvent);
            Boot(presentation: false);
            yield return AsyncAssert.WaitUntil(() => InSector("SectorA"), StepTimeoutSec, "the first sector starts");
            yield return Flush();

            Debug.Log($"[FMOD] presentation off: {MusicEvent} instances={Instances(description).Length}");
            Assert.AreEqual(0, Instances(description).Length, "no music instance without presentation");
            Assert.IsFalse(hostGo.GetComponentInChildren<GameMusic>(true), "no music object without presentation");
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private GameMusic NewMusic(IObjectiveService objectives)
        {
            var music = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameMusic>(MusicPrefabPath));
            created.Add(music.gameObject);
            music.Initialize(objectives);
            return music;
        }

        private static string Label(EventDescription description, EventInstance instance)
        {
            description.getParameterLabelByName(MusicStateName, (int)Parameter(instance, MusicStateName), out var label);
            return label;
        }

        private void Boot(bool presentation)
        {
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(SectorConfigPath);
            var templates = new[] { NewSectorTemplate("SectorA"), NewSectorTemplate("SectorB") };

            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                sectors = Array.ConvertAll(templates,
                    t => new SectorEntry { prefab = t.GetComponent<Sector>(), config = config }),
                presentation = presentation
            };
            host.playerRig = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.musicPrefab = AssetDatabase.LoadAssetAtPath<GameMusic>(MusicPrefabPath);
            recordPath = Path.Combine(Path.GetTempPath(), "game-music-" + Guid.NewGuid().ToString("N") + ".jsonl");
            host.runRecordPath = recordPath;
            host.buildIdentity = (out BuildIdentity identity, out string failure) =>
            {
                identity = new BuildIdentity { commit = "test-commit" };
                failure = null;
                return true;
            };
            hostGo.SetActive(true);
        }

        // Inactive, so the template never runs; the session instantiates it like a sector prefab.
        private GameObject NewSectorTemplate(string name)
        {
            var go = New(name);
            go.SetActive(false);
            var sector = go.AddComponent<Sector>();
            sector.SetManifest(null, null, new SectorModule[] { go.AddComponent<ExtractOnCueModule>() });
            return go;
        }

        private bool InSector(string templateName) =>
            host.ActiveSector && host.ActiveSector.name.StartsWith(templateName, StringComparison.Ordinal);

        // The sector loads under a holder, moving to the sector root as its last step.
        private bool Loaded(string templateName) =>
            InSector(templateName) && host.ActiveSector.transform.parent.name == "Sector";

        private void Dismiss()
        {
            var recap = hostGo.GetComponentInChildren<RunRecapScreen>();
            recap.transform.Find("Panels/Recap/Continue").GetComponent<Button>().onClick.Invoke();
        }

        // Each frame until the condition holds, records the MusicState label whenever it changes.
        private static IEnumerator Until(Func<bool> condition, string what, EventDescription description,
            List<string> seen, HashSet<int> instanceCounts)
        {
            return AsyncAssert.WaitUntil(() =>
            {
                RuntimeManager.StudioSystem.flushCommands();
                var instances = Instances(description);
                instanceCounts.Add(instances.Length);
                if (instances.Length == 1)
                {
                    var label = Label(description, instances[0]);
                    if (seen.Count == 0 || seen[seen.Count - 1] != label) seen.Add(label);
                }
                return condition();
            }, StepTimeoutSec, what);
        }
    }
}
#endif
