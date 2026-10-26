#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Runs;
using NUnit.Framework;
using UI.Recap;
using UI.Screens;
using Ships;
using Tests.PlayMode.Common;
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
        private const string FeelPath = "Assets/Settings/UI/RecapFeel.asset";

        private RunRecapScreen screen;
        private RecapFeelProfile feel;
        private float timeScale;
        private Keyboard keyboard;
        private GameObject playbackOwner;
        private RecapFeelProfile instantFeel;

        public override void Setup()
        {
            base.Setup();
            timeScale = Time.timeScale;
            keyboard = InputSystem.AddDevice<Keyboard>();
            feel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(FeelPath);
            Assert.IsTrue(feel, $"feel profile asset at {FeelPath}");
        }

        public override void TearDown()
        {
            if (screen) Object.Destroy(screen.gameObject);
            if (playbackOwner) Object.Destroy(playbackOwner);
            if (instantFeel) Object.Destroy(instantFeel);
            Time.timeScale = timeScale;
            base.TearDown();
        }

        private Text TextAt(string path) => screen.transform.Find("Panels/" + path).GetComponent<Text>();

        private void Play(RecapOutcomeContext context, RecapFeelProfile profile = null)
        {
            screen = RunRecapScreen.Create(null, context);
            screen.StartCoroutine(RecapPlayback.Run(new RecapOutcomeSequence(profile ? profile : feel, context),
                screen.Apply, () => screen.Continued));
        }

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
            Play(RecapOutcomeContext.Extraction(new[] { "Valis", "Crimson" }, 2, 222.8f));
            yield return null;
            Assert.AreEqual("EXTRACTED", TextAt("Recap/Outcome").text);
            Assert.AreEqual("2 CONFIRMED KILLS", TextAt("Recap/Kills").text);
            Assert.AreEqual("SECTOR 02 COMPLETE", TextAt("Recap/Context").text);
            Assert.AreEqual("3:42", TextAt("Recap/Time").text);
            Assert.IsNull(screen.transform.Find("Panels/Recap/Scoreboard"));
            Assert.IsNull(screen.transform.Find("OutcomeIntro"));
            Assert.That(screen.transform.Find("Panels/Recap").GetComponent<CanvasGroup>().alpha, Is.LessThan(1f),
                "the sector card fades in");
            yield return new WaitForSecondsRealtime(feel.BreakdownInSeconds + .1f);
            screen.transform.Find("Panels/Recap/Continue").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(screen.Continued);
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
            var kills = Enumerable.Repeat("Nightshade", 17).ToArray();
            var cleared = victory ? 3 : 2;
            var board = new Scoreboard(new[] { new Scoreboard.Row(3, 25, 900, DateTime.UtcNow) },
                new Scoreboard.Row(cleared, 17, 686, DateTime.UtcNow));
            Time.timeScale = 0f;
            Press(keyboard.spaceKey);
            Press(keyboard.wKey);
            Play(victory
                ? RecapOutcomeContext.Victory(kills, cleared, 3, board)
                : RecapOutcomeContext.Defeat(kills, cleared, 3, board, Vector2.zero));
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
            Assert.IsFalse(screen.Continued, "the opening outcome cannot dismiss the recap");
            Assert.AreEqual(0f, intro.GetComponent<CanvasGroup>().alpha);
            var revealDelay = victory ? feel.HudFadeSeconds : Mathf.Max(feel.HudFadeSeconds, feel.DeathRevealDelaySeconds);
            yield return new WaitForSecondsRealtime(revealDelay + feel.BannerInSeconds + .1f);
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
            yield return new WaitForSecondsRealtime(feel.BannerOutSeconds + feel.GapSeconds + feel.BreakdownInSeconds + .1f);
            Assert.AreEqual(1f, details.alpha, "the configured transition completes even while the key stays held");
            Assert.IsFalse(details.interactable, "the advancing press cannot also submit Continue");
            button.OnSubmit(null);
            Assert.IsFalse(screen.Continued);
            Release(keyboard.spaceKey);
            var deadline = Time.realtimeSinceStartup + .3f;
            while (!details.interactable && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.IsFalse(intro.gameObject.activeSelf, "the outcome fades away even while gameplay is paused");
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
            Assert.IsTrue(screen.Continued);
        }

        [UnityTest]
        public IEnumerator ZeroDurationReveal_ShowsTheCardBeforeRelease_WithoutSubmittingContinue()
        {
            instantFeel = Object.Instantiate(feel);
            var properties = new SerializedObject(instantFeel);
            foreach (var field in new[] { "hudFadeSeconds", "bannerInSeconds", "bannerOutSeconds", "gapSeconds", "breakdownInSeconds" })
                properties.FindProperty(field).floatValue = 0f;
            properties.ApplyModifiedPropertiesWithoutUndo();
            Play(RecapOutcomeContext.Victory(Array.Empty<string>(), 3, 3,
                new Scoreboard(Array.Empty<Scoreboard.Row>(), new Scoreboard.Row(3, 0, 60f, DateTime.UtcNow))), instantFeel);
            yield return null;
            Press(keyboard.enterKey);
            yield return null;
            yield return null;
            var details = screen.transform.Find("Panels/Recap").GetComponent<CanvasGroup>();
            Assert.AreEqual(1f, details.alpha);
            Assert.IsFalse(details.interactable);
            Assert.IsFalse(screen.Continued);
            Release(keyboard.enterKey);
            yield return null;
            yield return null;
            Assert.IsTrue(details.interactable);
            Assert.IsFalse(screen.Continued);
        }

        [UnityTest]
        public IEnumerator ExtractionScreenOnly_KeepsTheClockAndCompletesOnce()
        {
            var context = RecapOutcomeContext.Extraction(new[] { "Valis" }, 1, 42f);
            playbackOwner = new GameObject("PlaybackOwner");
            var runner = playbackOwner.AddComponent<RunRecapScreen>();
            var completed = 0;
            Time.timeScale = .65f;
            IEnumerator ShowCard()
            {
                yield return RecapPlayback.RunScreen(new RecapOutcomeSequence(feel, context), playbackOwner.transform);
                completed++;
            }
            runner.StartCoroutine(ShowCard());
            yield return null;
            screen = playbackOwner.transform.GetChild(0).GetComponent<RunRecapScreen>();
            yield return new WaitForSecondsRealtime(feel.BreakdownInSeconds + .1f);
            Assert.AreEqual(.65f, Time.timeScale);
            Assert.AreEqual(0, completed, "cruise waits for the real card's Continue");
            screen.transform.Find("Panels/Recap/Continue").GetComponent<Button>().OnSubmit(null);
            yield return new WaitForSecondsRealtime(feel.BreakdownOutSeconds + .1f);
            yield return null;
            Assert.AreEqual(1, completed);
            Assert.IsFalse(screen, "completion destroys the owned screen");
            Assert.AreEqual(.65f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator CancellationDuringThePressHold_DisposesInputAndTheScreen()
        {
            var context = RecapOutcomeContext.Victory(Array.Empty<string>(), 3, 3,
                new Scoreboard(Array.Empty<Scoreboard.Row>(), new Scoreboard.Row(3, 0, 60f, DateTime.UtcNow)));
            playbackOwner = new GameObject("PlaybackOwner");
            var runner = playbackOwner.AddComponent<RunRecapScreen>();
            var cancelled = false;
            var completed = false;
            IEnumerator ShowCard()
            {
                yield return RecapPlayback.RunScreen(new RecapOutcomeSequence(feel, context), playbackOwner.transform,
                    cancelled: () => cancelled);
                completed = true;
            }
            var actionsBefore = InputSystem.ListEnabledActions().Count;
            runner.StartCoroutine(ShowCard());
            yield return AsyncAssert.WaitUntil(() => InputSystem.ListEnabledActions().Count > actionsBefore,
                5f, "the banner is listening for input");
            screen = playbackOwner.transform.GetChild(0).GetComponent<RunRecapScreen>();
            Assert.Greater(InputSystem.ListEnabledActions().Count, actionsBefore, "the banner is listening for input");
            cancelled = true;
            yield return null;
            yield return null;
            Assert.IsTrue(completed);
            Assert.IsFalse(screen);
            Assert.AreEqual(actionsBefore, InputSystem.ListEnabledActions().Count);
        }

        [UnityTest]
        public IEnumerator LongRun_KeepsEveryTrophyAndTheCurrentRankVisible()
        {
            var previous = Enumerable.Range(0, 8).Select(i => new Scoreboard.Row(3, 100 + i, 900, DateTime.UtcNow)).ToArray();
            var board = new Scoreboard(previous, new Scoreboard.Row(2, 57, 600, DateTime.UtcNow));
            Play(RecapOutcomeContext.Defeat(Enumerable.Repeat("Bulwark", 57).ToArray(), 2, 3, board, Vector2.zero));
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
