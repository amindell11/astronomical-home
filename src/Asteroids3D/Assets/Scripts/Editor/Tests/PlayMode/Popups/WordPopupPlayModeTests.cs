using System.Collections;
using NUnit.Framework;
using UI.Popups;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Popups
{
    /// <summary>A word shows in its colour just above its world point's screen point, rises and fades out, then removes itself.</summary>
    [Category("UI")]
    public class WordPopupPlayModeTests
    {
        private GameObject root;
        private Camera worldCamera;
        private Canvas canvas;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Word popup");
            worldCamera = new GameObject("World camera").AddComponent<Camera>();
            worldCamera.transform.SetParent(root.transform, false);
            worldCamera.transform.position = new Vector3(0f, 0f, -10f);
            worldCamera.orthographic = true;
            canvas = new GameObject("HUD", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [UnityTest]
        public IEnumerator Word_ShowsAtTheWreck_RisesAndFades_ThenIsGone()
        {
            var wreck = new Vector3(2f, 1f, 0f);
            var popup = WordPopup.Show(canvas, worldCamera, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                null, "STUNNED", Color.yellow, wreck);
            var rect = (RectTransform)popup.transform;
            var text = popup.GetComponent<Text>();
            Vector2 wreckOnScreen = worldCamera.WorldToScreenPoint(wreck);

            Assert.AreEqual("STUNNED", text.text);
            Assert.AreEqual(Color.yellow, text.color);
            Assert.AreEqual(wreckOnScreen.x, rect.anchoredPosition.x * canvas.scaleFactor, .5f);
            Assert.AreEqual(wreckOnScreen.y + WordPopup.StartLiftPixels * canvas.scaleFactor,
                rect.anchoredPosition.y * canvas.scaleFactor, .5f, "It starts just above the wreck, clear of the fireball.");

            yield return new WaitForSeconds(WordPopup.Seconds * .8f);
            Assert.Greater(rect.anchoredPosition.y * canvas.scaleFactor, wreckOnScreen.y + (WordPopup.StartLiftPixels + 20f) * canvas.scaleFactor, "It keeps rising.");
            Assert.Less(text.color.a, 1f, "It fades near the end.");

            yield return new WaitForSeconds(WordPopup.Seconds * .4f);
            Assert.IsFalse(popup, "It removes itself once faded.");
        }
    }
}
