#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UI.Projection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Rendering.Projection
{
    /// <summary>
    /// The HUD is its own screen, so its pixel effects stay as they were; an image is its own
    /// screen until its owner says where it shows.
    /// </summary>
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class CanopyScreenPlayModeTests
    {
        private const string FeaturePath = "Assets/Settings/Rendering/CanopyProjectionFeature.asset";
        private GameObject root;
        private RenderTexture input;
        private RenderTexture hostTarget;

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            if (input) Object.DestroyImmediate(input);
            if (hostTarget) Object.DestroyImmediate(hostTarget);
        }

        // Batch mode renders a camera only into a target of its own.
        private Camera Host(string name)
        {
            root = new GameObject(name);
            var host = root.AddComponent<Camera>();
            root.AddComponent<UniversalAdditionalCameraData>();
            hostTarget = new RenderTexture(64, 32, 0, RenderTextureFormat.ARGB32);
            hostTarget.Create();
            host.targetTexture = hostTarget;
            return host;
        }

        [UnityTest]
        public IEnumerator TheHudProjection_IsItsOwnScreen()
        {
            var camera = Host("Canopy screen");
            var projection = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cameras/UI Cam.prefab"),
                root.transform).GetComponent<CanopyProjection>();
            projection.Initialize(camera);
            yield return null;

            var source = (ICanopySource)projection;
            var ink = source.Ink;
            Assert.That(source.Screen.Apparent, Is.EqualTo(new Vector2(ink.width, ink.height)));
            Assert.That(source.Screen.Frame, Is.EqualTo(new Vector2(ink.width, ink.height)));
            Assert.That(source.Screen.Apparent, Is.EqualTo(new Vector2(camera.pixelWidth, camera.pixelHeight)));
        }

        [UnityTest]
        public IEnumerator AnImage_IsItsOwnScreenUntold_AndReportsTheScreenAndGlitchItIsTold()
        {
            var host = Host("Canopy image host");
            host.cullingMask = 0;
            input = new RenderTexture(40, 24, 0, RenderTextureFormat.ARGB32);
            input.Create();

            var untold = Image();
            untold.Initialize(host, () => input, () => default);
            var screen = new CanopyScreen(new Vector2(10f, 6f), new Vector2(300f, 200f));
            var told = Image();
            told.Initialize(host, () => input, () => default, () => screen, () => .2f);
            yield return null;
            yield return null;

            var untoldSource = (ICanopySource)untold;
            Assert.That(untoldSource.Screen.Apparent, Is.EqualTo(new Vector2(40f, 24f)));
            Assert.That(untoldSource.Screen.Frame, Is.EqualTo(new Vector2(40f, 24f)));
            Assert.That(untoldSource.TransitionAge, Is.EqualTo(-1f));
            var toldSource = (ICanopySource)told;
            Assert.That(toldSource.Screen.Apparent, Is.EqualTo(screen.Apparent));
            Assert.That(toldSource.Screen.Frame, Is.EqualTo(screen.Frame));
            Assert.That(toldSource.TransitionAge, Is.EqualTo(.2f));
        }

        private CanopyImage Image()
        {
            var imageObject = new GameObject("Canopy image");
            imageObject.transform.SetParent(root.transform, false);
            imageObject.SetActive(false);
            var image = imageObject.AddComponent<CanopyImage>();
            using (var wiring = new SerializedObject(image))
            {
                wiring.FindProperty("feature").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CanopyProjectionFeature>(FeaturePath);
                wiring.ApplyModifiedPropertiesWithoutUndo();
            }
            imageObject.SetActive(true);
            return image;
        }
    }
}
#endif
