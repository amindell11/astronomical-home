using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cameras;
using Game;
using NUnit.Framework;
using Substrate.Results;
using Substrate.Sectors;
using Substrate.Services.Locales;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    [Category("Sectors"), Category("RequiresGraphics")]
    public sealed class PlanetPlaytestProbe : PlayModeWorldFixture
    {
        private GameHost host;
        private GameObject staging;
        private RenderTexture target;
        private Texture2D image;
        private RenderTexture previousTarget;
        private float previousTimeScale;

        public override void SetUp()
        {
            base.SetUp();
            previousTimeScale = Time.timeScale;
            previousTarget = RenderTexture.active;
            Time.timeScale = 1;
            target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        }

        [UnityTearDown]
        public IEnumerator ShutDownRemainingHost()
        {
            if (host) yield return host.Shutdown();
        }

        public override void TearDown()
        {
            RenderTexture.active = previousTarget;
            if (host) Object.DestroyImmediate(host.gameObject);
            if (staging) Object.DestroyImmediate(staging);
            if (target) target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            Time.timeScale = previousTimeScale;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator BothPlanetLocalesRenderBehindTheProductionPlayer()
        {
            var output = ResultsRoot.Folder("unity-tests-agent/locale-variety/revision-20261105b/playtest-captures");
            var records = ResultsRoot.Folder("unity-tests-agent/locale-variety/revision-20261105b/run-records");
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(records);
            var configs = new[] { "VerdantGiant", "BlueCrescent" };
            var template = AssetDatabase.LoadAssetAtPath<GameHost>("Assets/Prefabs/Game/GameHost.prefab");
            Assert.IsTrue(template);
            for (var index = 0; index < configs.Length; index++)
            {
                var id = index + 6;
                var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(
                    "Assets/Settings/Game/Preview/Locales/" + configs[index] + ".asset");
                Assert.IsTrue(config);
                staging = new GameObject("Locale playtest staging");
                staging.SetActive(false);
                host = Object.Instantiate(template, staging.transform);
                host.gameObject.SetActive(false);
                host.sessionProfile = new SessionProfile
                {
                    idleLocale = template.sessionProfile.idleLocale,
                    presentation = true,
                    offset = template.sessionProfile.offset,
                    sectors = new[] { new SectorEntry { prefab = template.sessionProfile.sectors[0].prefab, config = config } }
                };
                host.hangarScreenPrefab = null;
                host.musicPrefab = null;
                host.runRecordPath = Path.Combine(records, Guid.NewGuid().ToString("N") + ".jsonl");
                host.transform.SetParent(null, false);
                Object.Destroy(staging);
                staging = null;
                host.gameObject.SetActive(true);

                PlayerRig rig = null;
                var deadline = Time.realtimeSinceStartup + 30;
                while (Time.realtimeSinceStartup < deadline)
                {
                    rig = host.GetComponentInChildren<PlayerRig>();
                    if (host.ActiveSector && rig && rig.Player && rig.Player.gameObject.activeInHierarchy) break;
                    yield return null;
                }
                Assert.IsTrue(host.ActiveSector, configs[index] + " reached the first sector");
                Assert.IsTrue(rig && rig.Player && rig.Player.gameObject.activeInHierarchy,
                    configs[index] + " has the production player");
                var settle = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < settle) yield return null;
                var shown = Object.FindObjectsByType<LocaleSky>(FindObjectsSortMode.None).Where(locale => locale.Shown).ToArray();
                Assert.That(shown, Has.Length.EqualTo(1), "Only the destination locale may remain visible.");
                Assert.That(shown[0].name, Is.EqualTo("Locale_" + id));
                var observer = host.GetComponentInChildren<ObserverCam>();
                Assert.IsTrue(observer && observer.Cam);
                Capture(observer.Cam, Path.Combine(output, "Locale_" + id + "-playtest.png"));
                Debug.Log("LOCALE_PLAYTEST=" + id + " PLAYER=" + rig.Player.name + " SKY=" + shown[0].name);
                yield return host.Shutdown();
                Object.Destroy(host.gameObject);
                host = null;
                yield return null;
            }
        }

        private void Capture(Camera camera, string path)
        {
            var savedTarget = camera.targetTexture;
            var savedAspect = camera.aspect;
            try
            {
                camera.targetTexture = target;
                camera.aspect = 16f / 9;
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                var pixels = image.GetPixels32();
                Assert.That(pixels.Count(pixel => pixel.r + pixel.g + pixel.b > 40), Is.GreaterThan(10000));
                var luminance = pixels.Select(pixel => (int)pixel.r + pixel.g + pixel.b).ToArray();
                Assert.That(luminance.Max() - luminance.Min(), Is.GreaterThan(80), "A uniform capture cannot prove a locale renders.");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = savedTarget;
                camera.aspect = savedAspect;
                RenderTexture.active = previousTarget;
            }
        }
    }
}


