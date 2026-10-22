#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Runs;
using NUnit.Framework;
using UI.Screens;
using Ships;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    [Category("UI")]
    public class RunRecapScreensPlayModeTests : InputTestFixture
    {
        private RunRecapScreen screen;
        private float timeScale;
        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            timeScale = Time.timeScale;
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            if (screen) Object.Destroy(screen.gameObject);
            Time.timeScale = timeScale;
            base.TearDown();
        }

        private Text TextAt(string path) => screen.transform.Find("Panels/" + path).GetComponent<Text>();

        [Test]
        public void EveryPlayableChassis_HasANonEmptyTransparentSilhouette()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Ships" }))
            {
                var ship = AssetDatabase.LoadAssetAtPath<Ship>(AssetDatabase.GUIDToAssetPath(guid));
                if (!ship || ship.name == "ShipBase") continue;
                var icon = Resources.Load<Texture2D>(RunRecapScreen.SilhouetteFolder + "/" + ship.name);
                Assert.IsNotNull(icon, ship.name);
                var pixels = new Texture2D(2, 2);
                try
                {
                    pixels.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(icon)));
                    var ink = pixels.GetPixels32().Count(pixel => pixel.a > 128);
                    Assert.That(ink, Is.InRange(50, pixels.width * pixels.height / 2), ship.name);
                }
                finally { Object.DestroyImmediate(pixels); }
            }
        }

        [UnityTest]
        public IEnumerator SectorRecap_ShowsSectorKillsAndTime_WithoutScoreboard()
        {
            screen = RunRecapScreen.Create(null);
            var continued = false;
            screen.ShowSector(new[] { "Valis", "Crimson" }, 2, 222.8f, () => continued = true);
            yield return null;
            Assert.AreEqual("EXTRACTED", TextAt("Recap/Outcome").text);
            Assert.AreEqual("2 CONFIRMED KILLS", TextAt("Recap/Kills").text);
            Assert.AreEqual("SECTOR 02 COMPLETE", TextAt("Recap/Context").text);
            Assert.AreEqual("3:42", TextAt("Recap/Time").text);
            Assert.IsNull(screen.transform.Find("Panels/Recap/Scoreboard"));
            Assert.IsNull(screen.transform.Find("OutcomeIntro"));
            screen.transform.Find("Panels/Recap/Continue").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(continued);
        }

        [UnityTest]
        public IEnumerator Victory_ShowsGreenOutcomeAndScoreboardOnSameCard()
        {
            yield return RunEnding(true);
        }

        [UnityTest]
        public IEnumerator Defeat_ShowsRedOutcomeAndOnlyClearedSectors()
        {
            yield return RunEnding(false);
        }

        private IEnumerator RunEnding(bool victory)
        {
            screen = RunRecapScreen.Create(null);
            var kills = Enumerable.Repeat("Nightshade", 17).ToArray();
            var cleared = victory ? 3 : 2;
            var board = new Scoreboard(new[] { new Scoreboard.Row(3, 25, 900, DateTime.UtcNow) },
                new Scoreboard.Row(cleared, 17, 686, DateTime.UtcNow));
            var continued = false;
            Time.timeScale = 0f;
            Press(keyboard.spaceKey);
            Press(keyboard.wKey);
            screen.ShowRun(victory, kills, cleared, 3, board, () => continued = true);
            var details = screen.transform.Find("Panels/Recap").GetComponent<CanvasGroup>();
            var intro = screen.transform.Find("OutcomeIntro");
            var headline = intro.Find("Outcome").GetComponent<Text>();
            var button = screen.transform.Find("Panels/Recap/Continue").GetComponent<Button>();
            Assert.AreEqual(victory ? "VICTORY" : "SHIP LOST", headline.text);
            Assert.That(headline.fontSize, Is.GreaterThan(TextAt("Recap/Outcome").fontSize * 2));
            Assert.AreEqual(TextAt("Recap/Outcome").color, headline.color);
            Assert.AreEqual(0f, details.alpha);
            Assert.IsFalse(button.IsInteractable());
            button.OnSubmit(null);
            Assert.IsFalse(continued, "the opening outcome cannot dismiss the recap");
            Assert.AreEqual(0f, intro.GetComponent<CanvasGroup>().alpha);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(1f, intro.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(0f, details.alpha, "a combat key held through the banner cannot advance it");
            Release(keyboard.wKey);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(1f, intro.GetComponent<CanvasGroup>().alpha,
                "releasing one combat key while another stays held is not a fresh press");
            Release(keyboard.spaceKey);
            yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(0f, details.alpha, "releasing the held key does not advance the banner");
            Press(keyboard.spaceKey);
            yield return null;
            Release(keyboard.spaceKey);
            yield return new WaitForSecondsRealtime(.85f);
            Assert.AreEqual(0f, details.alpha, "the banner fades out before the breakdown begins");
            var deadline = Time.realtimeSinceStartup + 3f;
            while (!details.interactable && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.IsFalse(intro, "the outcome fades away even while gameplay is paused");
            Assert.AreEqual(1f, details.alpha);
            Assert.IsTrue(button.IsInteractable());
            Assert.AreEqual(0f, Time.timeScale);
            Canvas.ForceUpdateCanvases();
            var outcome = TextAt("Recap/Outcome");
            Assert.AreEqual(victory ? "VICTORY" : "SHIP LOST", outcome.text);
            Assert.That(victory ? outcome.color.g > outcome.color.r : outcome.color.r > outcome.color.g);
            Assert.AreEqual($"{cleared} SECTORS CLEARED", TextAt("Recap/Progress").text);
            Assert.AreEqual(17, screen.transform.Find("Panels/Recap/Trophies/Content").childCount);
            foreach (var marker in screen.GetComponentsInChildren<RecapSectorMarker>())
                Assert.IsNotNull(marker.canvasRenderer, "sector markers need a renderer");
            Assert.IsTrue(TextAt("Recap/Scoreboard/Row2/Run").text.Contains("This run"));
            Assert.AreEqual(cleared.ToString(), TextAt("Recap/Scoreboard/Row2/Sectors").text);
            var recap = (RectTransform)screen.transform.Find("Panels/Recap");
            var scoreboard = (RectTransform)recap.Find("Scoreboard");
            Assert.AreEqual(1, screen.transform.Find("Panels").childCount);
            Assert.AreSame(recap, scoreboard.parent);
            Assert.IsNull(scoreboard.GetComponent<Image>());
            var scoreBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(recap, scoreboard);
            var trophyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(recap, recap.Find("Trophies"));
            var buttonBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(recap, recap.Find("Continue"));
            Assert.GreaterOrEqual(scoreBounds.min.x, recap.rect.xMin);
            Assert.LessOrEqual(scoreBounds.max.x, recap.rect.xMax);
            Assert.Less(scoreBounds.max.y, trophyBounds.min.y);
            Assert.Greater(scoreBounds.min.y, buttonBounds.max.y);
            Assert.IsNull(screen.transform.Find("Panels/Recap/Time"));
            button.OnSubmit(null);
            Assert.IsTrue(continued);
        }

        [UnityTest]
        public IEnumerator LongRun_KeepsEveryTrophyAndTheCurrentRankVisible()
        {
            screen = RunRecapScreen.Create(null);
            var previous = Enumerable.Range(0, 8).Select(i => new Scoreboard.Row(3, 100 + i, 900, DateTime.UtcNow)).ToArray();
            var board = new Scoreboard(previous, new Scoreboard.Row(2, 57, 600, DateTime.UtcNow));
            screen.ShowRun(false, Enumerable.Repeat("Bulwark", 57).ToArray(), 2, 3, board, () => { });
            yield return null;
            var scroll = screen.transform.Find("Panels/Recap/Trophies").GetComponent<ScrollRect>();
            Assert.AreEqual(57, scroll.content.childCount);
            Assert.IsTrue(scroll.vertical);
            scroll.verticalNormalizedPosition = 0f;
            yield return null;
            Assert.Greater(scroll.content.anchoredPosition.y, 0f);
            Assert.IsTrue(TextAt("Recap/Scoreboard/Row9/Run").text.Contains("This run"));
            Assert.AreEqual("57", TextAt("Recap/Scoreboard/Row9/Kills").text);
        }
    }
}
#endif
