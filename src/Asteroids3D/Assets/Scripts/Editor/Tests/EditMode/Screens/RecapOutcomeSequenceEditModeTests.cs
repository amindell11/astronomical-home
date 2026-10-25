using System;
using Game.Runs;
using NUnit.Framework;
using UI.Recap;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Screens
{
    /// <summary>The recap sequence as a pure function: spans, holds, pause points and fade curves, read off the shipped feel asset.</summary>
    [Category("UI")]
    public class RecapOutcomeSequenceEditModeTests
    {
        private const string FeelPath = "Assets/Settings/UI/RecapFeel.asset";
        private static readonly Scoreboard Board =
            new(Array.Empty<Scoreboard.Row>(), new Scoreboard.Row(3, 4, 500f, DateTime.UtcNow));

        private RecapFeelProfile feel;

        [SetUp]
        public void SetUp()
        {
            feel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(FeelPath);
            Assert.IsTrue(feel, $"feel profile asset at {FeelPath}");
        }

        [Test]
        public void TheShippedProfile_CarriesTheSequenceTimings()
        {
            Assert.AreEqual(.3f, feel.HudFadeSeconds);
            Assert.AreEqual(.8f, feel.BannerInSeconds);
            Assert.AreEqual(.7f, feel.BannerOutSeconds);
            Assert.AreEqual(.3f, feel.GapSeconds);
            Assert.AreEqual(.8f, feel.BreakdownInSeconds);
        }

        [Test]
        public void AnOutcome_RunsSevenSpans_HeldByEffectsAPressAndContinue()
        {
            var defeat = new RecapOutcomeSequence(feel, RecapOutcomeContext.Defeat(new[] { "Crimson" }, 1, 3, Board, Vector2.zero));
            var victory = new RecapOutcomeSequence(feel, RecapOutcomeContext.Victory(new[] { "Crimson" }, 3, 3, Board));
            Assert.AreEqual(7, defeat.SpanCount);
            CollectionAssert.AreEqual(
                new[] { RecapPhase.HudFade, RecapPhase.BannerIn, RecapPhase.BannerHold, RecapPhase.BannerOut, RecapPhase.Gap, RecapPhase.BreakdownIn, RecapPhase.Breakdown },
                Phases(defeat));
            Assert.AreEqual(RecapHold.DeathEffects, defeat.Span(0).Hold, "a defeat's HUD fade waits for the explosion");
            Assert.AreEqual(RecapHold.None, victory.Span(0).Hold, "a victory's HUD fade waits for nothing");
            Assert.AreEqual(RecapHold.Press, victory.Span(2).Hold);
            Assert.AreEqual(RecapHold.Continue, victory.Span(6).Hold);
            Assert.AreEqual(.3f, victory.Span(0).Seconds);
            Assert.AreEqual(0f, victory.Span(2).Seconds, "a hold has no timed part");
        }

        [Test]
        public void TheHudFades_WithTheCameraHeld_AndOnlyVictoryFreezesPlayAtOnce()
        {
            var defeat = new RecapOutcomeSequence(feel, RecapOutcomeContext.Defeat(Array.Empty<string>(), 1, 3, Board, Vector2.zero));
            var victory = new RecapOutcomeSequence(feel, RecapOutcomeContext.Victory(Array.Empty<string>(), 3, 3, Board));

            var start = defeat.StateAt(0, 0f);
            Assert.AreEqual(1f, start.HudAlpha);
            Assert.AreEqual(1f, start.ShipHudAlpha);
            Assert.IsTrue(start.CameraHeld);
            Assert.IsFalse(start.SimulationFrozen, "the explosion keeps simulating");
            Assert.IsTrue(victory.StateAt(0, 0f).SimulationFrozen, "victory freezes combat before fading the HUD");

            var half = defeat.StateAt(0, .15f);
            Assert.AreEqual(.5f, half.HudAlpha, 1e-5f, "smootherstep's midpoint");
            Assert.AreEqual(half.HudAlpha, half.ShipHudAlpha);
            Assert.AreEqual(0f, defeat.StateAt(0, .3f).HudAlpha);
            Assert.AreEqual(0f, defeat.StateAt(0, 5f).HudAlpha, "the hold keeps the faded state");
            Assert.IsTrue(defeat.StateAt(1, 0f).SimulationFrozen, "the banner pauses play");
        }

        [Test]
        public void TheBannerAndBreakdown_FollowTheSequenceFades()
        {
            var sequence = new RecapOutcomeSequence(feel, RecapOutcomeContext.Victory(Array.Empty<string>(), 3, 3, Board));
            Assert.AreEqual(0f, sequence.StateAt(1, 0f).BannerAlpha);
            Assert.AreEqual(.5f, sequence.StateAt(1, .4f).BannerAlpha, 1e-5f, "smoothstep's midpoint");
            Assert.AreEqual(1f, sequence.StateAt(1, .8f).BannerAlpha);
            Assert.IsFalse(sequence.StateAt(1, .8f).BannerPrompt, "the prompt waits for the fade to finish");

            var hold = sequence.StateAt(2, 1f);
            Assert.AreEqual(1f, hold.BannerAlpha);
            Assert.IsTrue(hold.BannerPrompt);
            Assert.AreEqual(0f, hold.BreakdownAlpha);
            Assert.IsFalse(hold.BreakdownInteractable);

            Assert.AreEqual(.5f, sequence.StateAt(3, .35f).BannerAlpha, 1e-5f);
            Assert.IsFalse(sequence.StateAt(3, 0f).BannerPrompt, "the prompt goes with the press");
            Assert.AreEqual(0f, sequence.StateAt(4, .1f).BannerAlpha);
            Assert.AreEqual(0f, sequence.StateAt(4, .1f).BreakdownAlpha, "nothing shows in the gap");
            Assert.AreEqual(.5f, sequence.StateAt(5, .4f).BreakdownAlpha, 1e-5f);
            Assert.IsFalse(sequence.StateAt(5, .8f).BreakdownInteractable);

            var card = sequence.StateAt(6, 0f);
            Assert.AreEqual(1f, card.BreakdownAlpha);
            Assert.IsTrue(card.BreakdownInteractable);
            Assert.IsTrue(card.CameraHeld);
            Assert.IsTrue(card.SimulationFrozen);
        }

        [Test]
        public void AnExtraction_IsTheCardAlone_WithShipBarsStillShowing()
        {
            var sequence = new RecapOutcomeSequence(feel, RecapOutcomeContext.Extraction(new[] { "Valis" }, 2, 222.8f));
            Assert.AreEqual(1, sequence.SpanCount);
            Assert.AreEqual(RecapPhase.Breakdown, sequence.Span(0).Phase);
            Assert.AreEqual(RecapHold.Continue, sequence.Span(0).Hold);
            var state = sequence.StateAt(0, 0f);
            Assert.AreEqual(0f, state.HudAlpha, "the player's overlay hides");
            Assert.AreEqual(1f, state.ShipHudAlpha, "ship bars stay");
            Assert.IsFalse(state.CameraHeld, "the camera keeps following");
            Assert.IsTrue(state.SimulationFrozen);
            Assert.AreEqual(1f, state.BreakdownAlpha);
            Assert.IsTrue(state.BreakdownInteractable);
            Assert.AreEqual(0f, state.BannerAlpha);
        }

        [Test]
        public void AZeroSecondFade_IsInstant()
        {
            var instant = ScriptableObject.CreateInstance<RecapFeelProfile>();
            try
            {
                var so = new SerializedObject(instant);
                so.FindProperty("bannerInSeconds").floatValue = 0f;
                so.ApplyModifiedPropertiesWithoutUndo();
                var sequence = new RecapOutcomeSequence(instant, RecapOutcomeContext.Victory(Array.Empty<string>(), 3, 3, Board));
                Assert.AreEqual(0f, sequence.Span(1).Seconds);
                Assert.AreEqual(1f, sequence.StateAt(1, 0f).BannerAlpha);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instant);
            }
        }

        private static RecapPhase[] Phases(RecapOutcomeSequence sequence)
        {
            var phases = new RecapPhase[sequence.SpanCount];
            for (var i = 0; i < phases.Length; i++) phases[i] = sequence.Span(i).Phase;
            return phases;
        }
    }
}
