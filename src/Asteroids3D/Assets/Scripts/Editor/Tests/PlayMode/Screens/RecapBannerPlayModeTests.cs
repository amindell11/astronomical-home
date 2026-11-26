using System;
using System.Collections;
using System.Linq;
using Game.Runs;
using NUnit.Framework;
using UI.Recap;
using UI.Screens;
using UI.Screens.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
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

        private RunRecapScreen screen;

        private RecapOutcomeSequence Sequence(bool victory)
        {
            var feel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(FeelPath);
            var board = new Scoreboard(Array.Empty<Scoreboard.Row>(), new Scoreboard.Row(0, 0, 6f, DateTime.UtcNow));
            return new RecapOutcomeSequence(feel, victory
                ? RecapOutcomeContext.Victory(Array.Empty<string>(), 3, 3, board)
                : RecapOutcomeContext.Defeat(Array.Empty<string>(), 0, 3, board, Vector2.zero));
        }

        private Text Headline() => screen.GetComponentsInChildren<Text>(true).Single(text => text.name == "Outcome");

        private int Shown(string title) =>
            screen.GetComponentsInChildren<Text>().Count(text => text.text == title && text.color.a > 0f);

        // Where the letters sit and how big they draw, in the screen root's space.
        private (Vector2 centre, float size) Pose(Text text)
        {
            var rect = text.rectTransform;
            var local = text.alignment == TextAnchor.MiddleCenter ? rect.rect.center
                : new Vector2(rect.rect.xMin + text.preferredWidth * .5f, rect.rect.center.y);
            var centre = (Vector2)screen.transform.InverseTransformPoint(rect.TransformPoint(local));
            return (centre, text.fontSize * rect.lossyScale.x / screen.transform.lossyScale.x);
        }

        [UnityTest]
        public IEnumerator ShipLost_IsBareTextThatFadesIn_OnlyOnceTheDeathRevealIsOver()
        {
            var sequence = Sequence(false);
            screen = RunRecapScreen.Create(root.transform, sequence.Context, null);
            yield return null;

            var prompt = screen.transform.Find("OutcomeIntro");
            var reveal = sequence.Span(0).Seconds;
            foreach (var at in new[] { 0f, reveal * .5f, reveal })
            {
                screen.Apply(sequence.StateAt(0, at));
                Assert.AreEqual(0, Shown("SHIP LOST"), $"nothing shows {at:0.00} s into the death reveal");
                Assert.IsFalse(prompt.gameObject.activeSelf);
            }

            var bannerIn = sequence.Span(1).Seconds;
            screen.Apply(sequence.StateAt(1, bannerIn * .1f));
            var headline = Headline();
            Assert.IsNull(headline.GetComponentInParent<ScreenWindow>(), "the banner is HUD text, not a window");
            Assert.AreSame(screen.transform, headline.transform.parent, "bare text on the ink root, no frame or box");
            Assert.That(headline.color.a, Is.GreaterThan(0f).And.LessThan(.05f), "it fades in rather than arriving whole");
            screen.Apply(sequence.StateAt(1, bannerIn));
            Assert.AreEqual(1f, headline.color.a);
        }

        [UnityTest]
        public IEnumerator TheBannerText_TravelsIntoTheRecorder_AndBecomesItsHeadline([Values(false, true)] bool victory)
        {
            var sequence = Sequence(victory);
            var title = victory ? "VICTORY" : "SHIP LOST";
            screen = RunRecapScreen.Create(root.transform, sequence.Context, null);
            yield return null;

            screen.Apply(sequence.StateAt(sequence.SpanCount - 1, 0f));
            var headline = Headline();
            Assert.AreSame(screen.transform.Find("Panels/Recap/Body"), headline.transform.parent);
            Assert.AreEqual(1f, headline.rectTransform.localScale.x, "at rest it draws at its own font size");
            var rest = Pose(headline);

            screen.Apply(sequence.StateAt(2, 0f));
            var previous = Pose(headline);
            Assert.That(previous.size, Is.GreaterThan(rest.size * 1.5f), "the banner is the big version");
            var outIndex = Array.IndexOf(Phases(sequence), RecapPhase.BannerOut);
            screen.Apply(sequence.StateAt(outIndex, sequence.Span(outIndex).Seconds * .5f));
            Assert.AreEqual(1f, headline.color.a, "the text stays while the prompt fades");
            Assert.AreEqual(previous.centre, Pose(headline).centre);

            var inIndex = Array.IndexOf(Phases(sequence), RecapPhase.BreakdownIn);
            var length = sequence.Span(inIndex).Seconds;
            for (var step = 0; step <= 20; step++)
            {
                screen.Apply(sequence.StateAt(inIndex, length * Mathf.Min(step / 20f, .999f)));
                var pose = Pose(headline);
                Assert.AreEqual(1, Shown(title), "one outcome text through the whole move");
                Assert.That(pose.size, Is.LessThanOrEqualTo(previous.size + 1e-3f));
                Assert.That(Vector2.Distance(pose.centre, rest.centre),
                    Is.LessThanOrEqualTo(Vector2.Distance(previous.centre, rest.centre) + 1e-3f));
                previous = pose;
            }
            Assert.That(Vector2.Distance(previous.centre, rest.centre), Is.LessThan(2f), "no jump at the hand-off");
            Assert.That(previous.size, Is.EqualTo(rest.size).Within(1f));

            screen.Apply(sequence.StateAt(inIndex + 1, 0f));
            Assert.AreEqual(1, Shown(title));
            Assert.AreSame(screen.transform.Find("Panels/Recap/Body"), headline.transform.parent);
        }

        private static RecapPhase[] Phases(RecapOutcomeSequence sequence) =>
            Enumerable.Range(0, sequence.SpanCount).Select(i => sequence.Span(i).Phase).ToArray();
    }
}
