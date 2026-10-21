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
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    [Category("UI")]
    public class RunRecapScreensPlayModeTests
    {
        private RunRecapScreen screen;

        [TearDown]
        public void TearDown()
        {
            if (screen) Object.Destroy(screen.gameObject);
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
            Assert.IsNull(screen.transform.Find("Panels/Scoreboard"));
            screen.transform.Find("Panels/Recap/Continue").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(continued);
        }

        [UnityTest]
        public IEnumerator Victory_ShowsGreenOutcomeAndScoreboardAtRecapHeight()
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
            screen.ShowRun(victory, kills, cleared, 3, board, () => continued = true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var outcome = TextAt("Recap/Outcome");
            Assert.AreEqual(victory ? "VICTORY" : "SHIP LOST", outcome.text);
            Assert.That(victory ? outcome.color.g > outcome.color.r : outcome.color.r > outcome.color.g);
            Assert.AreEqual($"{cleared} SECTORS CLEARED", TextAt("Recap/Progress").text);
            Assert.AreEqual(17, screen.transform.Find("Panels/Recap/Trophies/Content").childCount);
            foreach (var marker in screen.GetComponentsInChildren<RecapSectorMarker>())
                Assert.IsNotNull(marker.canvasRenderer, "sector markers need a renderer");
            Assert.IsTrue(TextAt("Scoreboard/Row2/Run").text.Contains("This run"));
            Assert.AreEqual(cleared.ToString(), TextAt("Scoreboard/Row2/Sectors").text);
            var recap = (RectTransform)screen.transform.Find("Panels/Recap");
            var scoreboard = (RectTransform)screen.transform.Find("Panels/Scoreboard");
            Assert.AreEqual(recap.rect.height, scoreboard.rect.height, .01f);
            Assert.AreEqual(recap.anchoredPosition.y, scoreboard.anchoredPosition.y, .01f);
            Assert.IsNull(screen.transform.Find("Panels/Recap/Time"));
            screen.transform.Find("Panels/Recap/Continue").GetComponent<Button>().onClick.Invoke();
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
            Assert.IsTrue(TextAt("Scoreboard/Row9/Run").text.Contains("This run"));
            Assert.AreEqual("57", TextAt("Scoreboard/Row9/Kills").text);
        }
    }
}
#endif
