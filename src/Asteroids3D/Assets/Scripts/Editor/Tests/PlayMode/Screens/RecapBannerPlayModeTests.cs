using System;
using System.Collections;
using Game.Runs;
using NUnit.Framework;
using UI.Recap;
using UI.Screens;
using UI.Screens.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    [Category("UI"), Category("RequiresLfsPng")]
    public class RecapBannerPlayModeTests
    {
        private const string FeelPath = "Assets/Settings/UI/RecapFeel.asset";

        private GameObject root;

        [SetUp]
        public void SetUp() => root = new GameObject("UI");

        [TearDown]
        public void TearDown() => Object.Destroy(root);

        [UnityTest]
        public IEnumerator ShipLost_IsBareTextThatFadesIn_OnlyOnceTheDeathRevealIsOver()
        {
            var feel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(FeelPath);
            var board = new Scoreboard(Array.Empty<Scoreboard.Row>(), new Scoreboard.Row(0, 0, 6f, DateTime.UtcNow));
            var sequence = new RecapOutcomeSequence(feel,
                RecapOutcomeContext.Defeat(Array.Empty<string>(), 0, 3, board, Vector2.zero));
            var screen = RunRecapScreen.Create(root.transform, sequence.Context, null);
            yield return null;

            var banner = screen.transform.Find("OutcomeIntro");
            Assert.IsNull(banner.GetComponentInChildren<ScreenWindow>(true), "the banner is HUD text, not a window");
            Assert.IsNull(banner.GetComponentInChildren<ChamferedShape>(true), "no frame, bar or body around it");

            var reveal = sequence.Span(0).Seconds;
            foreach (var at in new[] { 0f, reveal * .5f, reveal })
            {
                screen.Apply(sequence.StateAt(0, at));
                Assert.IsFalse(banner.gameObject.activeSelf, $"nothing shows {at:0.00} s into the death reveal");
            }

            var fade = banner.GetComponent<CanvasGroup>();
            var bannerIn = sequence.Span(1).Seconds;
            screen.Apply(sequence.StateAt(1, bannerIn * .1f));
            Assert.IsTrue(banner.gameObject.activeSelf);
            Assert.That(fade.alpha, Is.GreaterThan(0f).And.LessThan(.05f), "it fades in rather than arriving whole");
            screen.Apply(sequence.StateAt(1, bannerIn));
            Assert.AreEqual(1f, fade.alpha);
        }
    }
}
