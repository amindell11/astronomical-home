#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Substrate.Presentation;
using Substrate.Services.Environment.Flat;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    [Category("Sectors")]
    public sealed class FlatBackgroundPlayModeTests
    {
        private GameObject cameraRoot;
        private GameObject environmentRoot;
        private Scene original;
        private Scene locale;
        private Texture2D texture;
        private FlatBackgroundAsset background;
        private RenderTexture target;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            original = SceneManager.GetActiveScene();
            locale = SceneManager.CreateScene("Flat background test");
            SceneManager.SetActiveScene(locale);
            texture = new Texture2D(4, 4, TextureFormat.RGBAHalf, false, true);
            var colors = new Color[16];
            for (var i = 0; i < colors.Length; i++) colors[i] = new Color(2, 0.25f, 0.125f, 1);
            texture.SetPixels(colors);
            texture.Apply();
            background = ScriptableObject.CreateInstance<FlatBackgroundAsset>();
            background.Initialize(texture, true, Color.red, Color.green, Color.blue, Color.white, "{}");
            cameraRoot = new GameObject("Flight camera");
            cameraRoot.AddComponent<Camera>();
            cameraRoot.AddComponent<FlatBackgroundCamera>();
            environmentRoot = new GameObject("Environment");
            environmentRoot.SetActive(false);
            var authoring = environmentRoot.AddComponent<EnvironmentAuthoring>();
            var light = new GameObject("Native light").AddComponent<Light>();
            light.transform.SetParent(environmentRoot.transform);
            var serialized = new SerializedObject(authoring);
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("backgroundShader").objectReferenceValue = Shader.Find("Environment/Flat Background");
            var lights = serialized.FindProperty("nativeLights");
            lights.arraySize = 1;
            lights.GetArrayElementAtIndex(0).objectReferenceValue = light;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            environmentRoot.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (cameraRoot) Object.Destroy(cameraRoot);
            if (environmentRoot) Object.Destroy(environmentRoot);
            if (background) Object.Destroy(background);
            if (texture) Object.Destroy(texture);
            if (target) { target.Release(); Object.Destroy(target); }
            SceneManager.SetActiveScene(original);
            yield return SceneManager.UnloadSceneAsync(locale);
        }

        [UnityTest]
        public IEnumerator InactiveLocaleDisablesNativeLightsAndRestoresAuthoredState()
        {
            var light = environmentRoot.GetComponentInChildren<Light>();
            Assert.That(light.enabled, Is.True);
            SceneManager.SetActiveScene(original);
            yield return null;
            Assert.That(light.enabled, Is.False);
            SceneManager.SetActiveScene(locale);
            yield return null;
            Assert.That(light.enabled, Is.True);
        }

        [UnityTest, Category("RequiresGraphics")]
        public IEnumerator FlightCameraDrawsHdr_AndInactiveLocaleAndHeadlessDoNotDraw()
        {
            var camera = cameraRoot.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = true;
            target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBHalf);
            target.Create();
            camera.targetTexture = target;
            yield return null;
            yield return null;
            Assert.That(ReadCenter().r, Is.GreaterThan(1.5f), "Native flat clouds must reach the camera as HDR.");
            camera.orthographicSize = 100;
            camera.transform.position = new Vector3(-400000, 800000, -10);
            yield return null;
            Assert.That(ReadCenter().r, Is.GreaterThan(1.5f));
            SceneManager.SetActiveScene(original);
            yield return null;
            Assert.That(ReadCenter().r, Is.LessThan(0.01f), "Inactive loaded locales must not draw.");
            SceneManager.SetActiveScene(locale);
            PresentationApplier.Apply(cameraRoot, false);
            yield return null;
            Assert.That(ReadCenter().r, Is.LessThan(0.01f), "Presentation-off must suppress the background.");
            PresentationApplier.Apply(cameraRoot, true);
            yield return null;
            Assert.That(ReadCenter().r, Is.GreaterThan(1.5f));
        }

        private Color ReadCenter()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            readback.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
            readback.Apply();
            var result = readback.GetPixel(0, 0);
            Object.Destroy(readback);
            RenderTexture.active = previous;
            return result;
        }
    }
}
#endif
