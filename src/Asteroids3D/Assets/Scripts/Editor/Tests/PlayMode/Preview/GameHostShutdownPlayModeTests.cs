using System;
using System.Collections;
using System.IO;
using Cameras;
using Game;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Preview
{
    [Category("Bootstrap")]
    public sealed class GameHostShutdownPlayModeTests : PlayModeWorldFixture
    {
        private GameHost host;
        private GameObject template;
        private string recordPath;

        [UnityTearDown]
        public IEnumerator ShutdownAfterTest()
        {
            if (host)
            {
                if (host.ActiveSector)
                {
                    var suspended = host.ActiveSector.GetComponent<SuspendedSetupModule>();
                    if (suspended) suspended.AllowFinish = true;
                }
                yield return host.Shutdown();
                Object.Destroy(host.gameObject);
            }
            if (template) Object.Destroy(template);
            yield return null;
            if (File.Exists(recordPath)) File.Delete(recordPath);
        }

        [UnityTest]
        public IEnumerator ShutdownDuringBoot_CompletesCompositionBeforeTeardown()
        {
            BuildHost(withPlayer: false, suspendSetup: false);
            yield return host.Shutdown();
            Assert.That(host.ActiveSector, Is.Null);
            Assert.That(host.transform.childCount, Is.Zero);
            Assert.That(File.Exists(recordPath), Is.False);
            yield return host.Shutdown();
            Assert.That(host.transform.childCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ShutdownDuringSectorSetup_WaitsForTheOwnedOperation()
        {
            BuildHost(withPlayer: false, suspendSetup: true);
            yield return AsyncAssert.WaitUntil(() => host.ActiveSector &&
                host.ActiveSector.GetComponent<SuspendedSetupModule>().Entered, 30f, "sector setup entered");
            var sector = host.ActiveSector;
            var suspended = sector.GetComponent<SuspendedSetupModule>();
            var completed = false;
            IEnumerator RequestShutdown()
            {
                yield return host.Shutdown();
                completed = true;
            }
            host.StartCoroutine(RequestShutdown());
            yield return null;
            Assert.That(completed, Is.False, "shutdown waits for the pending setup");
            Assert.That(sector, Is.Not.Null);
            Assert.That(suspended.Finished, Is.False);
            suspended.AllowFinish = true;
            yield return AsyncAssert.WaitUntil(() => completed, 30f, "shutdown completed after setup released");
            Assert.That(sector == null, Is.True, "the completed sector is destroyed before shutdown returns");
            Assert.That(host.transform.childCount, Is.Zero);
            Assert.That(File.Exists(recordPath), Is.False);
        }

        [UnityTest]
        public IEnumerator ShutdownDuringFlight_RemovesTheRigAndWorldWithoutRecordingAnInterruptedRun()
        {
            BuildHost(withPlayer: true, suspendSetup: false);
            PlayerRig rig = null;
            yield return AsyncAssert.WaitUntil(() =>
            {
                rig = host.GetComponentInChildren<PlayerRig>();
                return rig && rig.Tally.SecondsSurvived > 0f;
            }, 30f, "the production run started");
            var player = rig.Player;
            var observer = host.GetComponentInChildren<ObserverCam>();
            yield return host.Shutdown();
            Assert.That(player == null, Is.True);
            Assert.That(rig == null, Is.True);
            Assert.That(observer == null, Is.True);
            Assert.That(host.transform.childCount, Is.Zero);
            Assert.That(File.Exists(recordPath), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        private void BuildHost(bool withPlayer, bool suspendSetup)
        {
            recordPath = Path.Combine(Path.GetTempPath(), "preview-shutdown-" + Guid.NewGuid().ToString("N") + ".jsonl");
            template = new GameObject("Shutdown test sector");
            template.SetActive(false);
            var sector = template.AddComponent<Sector>();
            sector.SetManifest(null, null, suspendSetup
                ? new SectorModule[] { template.AddComponent<SuspendedSetupModule>() }
                : Array.Empty<SectorModule>());
            var root = new GameObject("Shutdown test host");
            root.SetActive(false);
            host = root.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                presentation = false,
                sectors = new[]
                {
                    new SectorEntry
                    {
                        prefab = sector,
                        config = AssetDatabase.LoadAssetAtPath<SectorSettings>("Assets/Settings/Game/DefaultSectorConfig.asset")
                    }
                }
            };
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>("Assets/Prefabs/Cameras/Main Camera.prefab");
            if (withPlayer)
                host.playerRig = AssetDatabase.LoadAssetAtPath<PlayerRig>("Assets/Prefabs/MiscObjects/PlayerRig.prefab");
            host.runRecordPath = recordPath;
            root.SetActive(true);
        }
    }
}
