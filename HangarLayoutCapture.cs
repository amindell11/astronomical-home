#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using UI.Hangar.Hardware;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Hangar
{
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class HangarLayoutCapture
    {
        private const string Output = "C:/Users/amind/.codex/visualizations/2026/11/15/01a1dc89-c32a-7c82-ba76-5ae7751e1d5f/hangar";
        private HangarConsole console;
        private ItemSubset offer;
        private Texture2D shot;
        private Scene scene;
        private Scene previousScene;

        [UnityTest]
        public IEnumerator CapturesSettledHardwareAndLaunchHit()
        {
            previousScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("HangarLayoutCapture");
            SceneManager.SetActiveScene(scene);
            var vanguard = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");
            Assert.IsNotNull(vanguard);
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.starter = vanguard;
            offer.ships = new[] { vanguard };
            console = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(
                "Assets/Prefabs/UI/HangarConsole.prefab"));
            console.Show(offer, new ShipLoadout(vanguard, null, null), _ => true, null);

            var started = Time.unscaledTime;
            while (Time.unscaledTime - started < 3f) yield return null;
            for (var i = 0; i < 5; i++) yield return null;

            Assert.IsNotNull(console.Texture);
            shot = Read(console);
            var camera = console.HardwareCamera;
            var lensWorld = console.ProjectorLens.transform.position;
            var launchWorld = console.LaunchButton.GetComponent<Renderer>().bounds.center;
            var lensViewport = camera.WorldToViewportPoint(lensWorld);
            var launchViewport = camera.WorldToViewportPoint(launchWorld);
            var launchHit = console.GetComponent<HangarConsoleRaycaster>().HitAt(
                new Vector2(launchViewport.x, launchViewport.y));
            var pixels = shot.GetPixels32();
            var minimum = 1f;
            var maximum = 0f;
            double sum = 0;
            double sumSquares = 0;
            var visiblePixels = 0;
            foreach (var pixel in pixels)
            {
                var level = (pixel.r + pixel.g + pixel.b) / 765f;
                minimum = Mathf.Min(minimum, level);
                maximum = Mathf.Max(maximum, level);
                sum += level;
                sumSquares += level * level;
                if (pixel.a > 0) visiblePixels++;
            }
            var mean = sum / pixels.Length;
            var evidence = new Evidence
            {
                frame = Time.frameCount,
                elapsed = Time.unscaledTime - started,
                width = shot.width,
                height = shot.height,
                cameraPosition = camera.transform.position,
                cameraRotation = camera.transform.rotation,
                verticalFieldOfView = camera.fieldOfView,
                cameraAspect = camera.aspect,
                lensWorld = lensWorld,
                lensViewport = lensViewport,
                launchWorld = launchWorld,
                launchViewport = launchViewport,
                projectorWorld = console.Projector.transform.position,
                projectorLensDistance = Vector3.Distance(console.Projector.transform.position, lensWorld),
                launchHit = launchHit ? launchHit.name : "none",
                launchHitMatches = launchHit == console.LaunchButton.gameObject,
                minimumBrightness = minimum,
                maximumBrightness = maximum,
                brightnessVariance = (float)(sumSquares / pixels.Length - mean * mean),
                visiblePixels = visiblePixels,
                totalPixels = pixels.Length
            };
            Directory.CreateDirectory(Output);
            File.WriteAllBytes(Path.Combine(Output, "unity-hardware.png"), shot.EncodeToPNG());
            File.WriteAllText(Path.Combine(Output, "unity-hardware.json"), JsonUtility.ToJson(evidence, true));

            Assert.That(lensViewport.z, Is.GreaterThan(0f));
            Assert.That(launchViewport.z, Is.GreaterThan(0f));
            Assert.That(lensViewport.x, Is.EqualTo(.5f).Within(.01f), "the projector is centered");
            Assert.That(launchViewport.x, Is.EqualTo(.5f).Within(.01f), "Launch is centered");
            Assert.That(evidence.projectorLensDistance, Is.LessThan(.01f), "the projector follows its lens");
            Assert.IsTrue(evidence.launchHitMatches, "the relocated Launch button remains clickable");
            Assert.That(maximum - minimum, Is.GreaterThan(.1f), "the hardware capture contains visible variation");
            Assert.That(evidence.brightnessVariance, Is.GreaterThan(.0001f), "the capture is not uniformly blank");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (shot) Object.Destroy(shot);
            if (console) Object.Destroy(console.gameObject);
            if (offer) Object.Destroy(offer);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            yield return null;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static Texture2D Read(HangarConsole sourceConsole)
        {
            var source = (RenderTexture)sourceConsole.Texture;
            var readable = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, readable);
                RenderTexture.active = readable;
                var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(readable);
            }
        }

        [Serializable]
        private sealed class Evidence
        {
            public int frame;
            public float elapsed;
            public int width;
            public int height;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public float verticalFieldOfView;
            public float cameraAspect;
            public Vector3 lensWorld;
            public Vector3 lensViewport;
            public Vector3 launchWorld;
            public Vector3 launchViewport;
            public Vector3 projectorWorld;
            public float projectorLensDistance;
            public string launchHit;
            public bool launchHitMatches;
            public float minimumBrightness;
            public float maximumBrightness;
            public float brightnessVariance;
            public int visiblePixels;
            public int totalPixels;
        }
    }
}
#endif
