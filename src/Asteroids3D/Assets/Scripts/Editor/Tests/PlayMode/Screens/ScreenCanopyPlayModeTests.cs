#if UNITY_EDITOR
using System;
using System.Collections;
using Combat.Weapons;
using Loot;
using NUnit.Framework;
using UI.Projection;
using UI.Recap;
using UI.Screens;
using UI.Screens.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    /// <summary>Through the canopy a screen draws its light on the ink camera and its fade and window bodies on the gameplay camera, which follow their windows; opening a window tears the canopy and dips the window's ink.</summary>
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class ScreenCanopyPlayModeTests
    {
        private GameObject root;
        private Camera sceneCamera;
        private CanopyProjection projection;
        private CanopyProjectionProfile profile;
        private ScreenCameras cameras;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Screen canopy");
            sceneCamera = new GameObject("Scene camera", typeof(Camera), typeof(UniversalAdditionalCameraData))
                .GetComponent<Camera>();
            sceneCamera.transform.SetParent(root.transform, false);
            projection = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cameras/UI Cam.prefab"), root.transform)
                .GetComponent<CanopyProjection>();
            profile = Object.Instantiate(projection.Profile);
            profile.personalityEnabled = true;
            profile.baseSettings.flicker = 1f;
            profile.windowFlicker = 1f;
            using (var serialized = new SerializedObject(projection))
            {
                serialized.FindProperty("profile").objectReferenceValue = profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            projection.Initialize(sceneCamera);
            cameras = new ScreenCameras(projection.Camera, sceneCamera, projection);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(profile);
        }

        private static WeaponComponent Weapon(string name) =>
            AssetDatabase.LoadAssetAtPath<WeaponComponent>($"Assets/Prefabs/Weapons/{name}.prefab");

        private WeaponPickScreen Pick() => WeaponPickScreen.Create(root.transform,
            new[] { Weapon("Lasers"), Weapon("Grenades") }, Weapon("Rippers"), null, new LullModStack(), cameras);

        [UnityTest]
        public IEnumerator AWeaponPick_DrawsItsLightOnTheInkCamera_AndItsFadeAndBodiesOnTheGameplayCamera()
        {
            var screen = Pick();
            yield return null;

            var inkLayer = LayerMask.NameToLayer("UIBloom");
            var ink = screen.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, ink.renderMode);
            Assert.AreSame(projection.Camera, ink.worldCamera);
            Assert.AreEqual(inkLayer, screen.gameObject.layer);
            Assert.AreEqual(inkLayer, screen.transform.Find("Offers/Cards/Card1/Body/Name").gameObject.layer);
            Assert.AreEqual(Color.black, screen.transform.Find("Offers/Cards/Card1/Back").GetComponent<ChamferedShape>().color,
                "the ink body adds no light and hides what is behind it");

            var under = screen.GetComponent<ScreenLayers>().Under;
            Assert.AreNotSame(screen.transform, under);
            var underCanvas = under.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, underCanvas.renderMode);
            Assert.AreSame(sceneCamera, underCanvas.worldCamera);
            Assert.AreEqual(LayerMask.NameToLayer("UI"), under.gameObject.layer);
            Assert.AreEqual(ink.planeDistance, underCanvas.planeDistance, "the two canvases coincide");
            Assert.IsNull(under.GetComponent<GraphicRaycaster>(), "clicks stay on the ink canvas");
            Assert.IsNotNull(under.Find("Fade").GetComponent<Image>());
            Assert.IsNull(screen.transform.Find("Fade"));
            Assert.AreEqual(3, under.GetComponentsInChildren<WindowShade>().Length, "two cards and the skip");
        }

        [UnityTest]
        public IEnumerator AWindowShade_FollowsItsWindowsUnfold_AndTheFadeOfTheGroupAboveIt()
        {
            var screen = RunRecapScreen.Create(root.transform, RecapOutcomeContext.Extraction(Array.Empty<string>(), 1, 42f),
                cameras);
            var window = (RectTransform)screen.transform.Find("Panels/Recap");
            var recap = window.GetComponent<ScreenWindow>();
            var group = screen.transform.Find("Panels").GetComponent<CanvasGroup>();
            var shade = (RectTransform)screen.GetComponent<ScreenLayers>().Under.Find("Recap Shade");
            var shape = shade.GetComponent<ChamferedShape>();
            group.alpha = 1f;

            recap.Open = .3f;
            // A new canvas takes its size when it first renders, a frame after the shade first follows.
            yield return null;
            yield return null;
            var clip = window.GetComponent<RectMask2D>().padding;
            Assert.That(clip.y, Is.GreaterThan(0f), "a window at .3 is still unfolding");
            Assert.That(shade.rect.width, Is.EqualTo(window.rect.width - clip.x - clip.z).Within(.01f));
            Assert.That(shade.rect.height, Is.EqualTo(window.rect.height - clip.y - clip.w).Within(.01f));
            // Both canvases share one scaler, so a point in ink-canvas coordinates is the same point under it.
            var centre = screen.transform.InverseTransformPoint(window.TransformPoint(
                window.rect.center + new Vector2(clip.x - clip.z, clip.y - clip.w) * .5f));
            Assert.That(shade.localPosition.x, Is.EqualTo(centre.x).Within(.01f));
            Assert.That(shade.localPosition.y, Is.EqualTo(centre.y).Within(.01f));
            Assert.IsTrue(shape.enabled);

            recap.Open = 1f;
            yield return null;
            Assert.That(shade.rect.width, Is.EqualTo(window.rect.width).Within(.01f));
            Assert.That(shade.rect.height, Is.EqualTo(window.rect.height).Within(.01f));
            Assert.That(shade.localPosition.x,
                Is.EqualTo(screen.transform.InverseTransformPoint(window.TransformPoint(window.rect.center)).x).Within(.01f));

            group.alpha = 0f;
            yield return null;
            Assert.IsFalse(shape.enabled, "a hidden group's window has no body");
        }

        [UnityTest]
        public IEnumerator OpeningAWindow_TearsTheCanopy_AndDipsItsInkOnlyInTheAppearanceDips()
        {
            var screen = Pick();
            var flicker = screen.transform.Find("Offers/Cards/Card1").GetComponent<CanvasGroup>();
            Assert.That(projection.GlitchAge, Is.LessThan(0f));
            yield return null;
            var opened = Time.unscaledTime;
            yield return null;
            Assert.That(projection.GlitchAge, Is.GreaterThanOrEqualTo(0f), "the window's opening tears the canopy");
            Assert.That(projection.GlitchStrength, Is.EqualTo(profile.windowGlitch));

            var sampled = opened;
            var dipped = false;
            while (sampled - opened < .6f)
            {
                var age = sampled - opened;
                // A frame either side, since the opening frame is only known to the test to within one.
                var inDip = age >= .04f && age < .24f || age >= .34f && age < .54f;
                if (flicker.alpha < 1f)
                {
                    Assert.IsTrue(inDip, $"a dip at {age:0.000}s falls in the envelope's dips");
                    Assert.That(flicker.alpha, Is.EqualTo(1f - .17f).Within(.001f));
                    dipped = true;
                }
                sampled = Time.unscaledTime;
                yield return null;
            }
            Assert.IsTrue(dipped, "the ink dips while the window opens");
        }

        [UnityTest]
        public IEnumerator DestroyingAScreen_RemovesItsUnderCanvas()
        {
            var screen = Pick();
            var under = screen.GetComponent<ScreenLayers>().Under;
            yield return null;
            Object.Destroy(screen.gameObject);
            yield return null;
            Assert.IsFalse(under);
        }
    }
}
#endif
