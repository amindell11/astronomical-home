using System;
using UnityEngine;

namespace UI.Recap
{
    public enum RecapPhase { HudFade, BannerIn, BannerHold, BannerOut, Gap, BreakdownIn, Breakdown }

    /// <summary>What ends a span once its timed part is over.</summary>
    public enum RecapHold { None, Press, Release, Continue }

    public readonly struct RecapSpan
    {
        public RecapPhase Phase { get; }
        public float Seconds { get; }
        public RecapHold Hold { get; }

        public RecapSpan(RecapPhase phase, float seconds, RecapHold hold)
        {
            Phase = phase;
            Seconds = seconds;
            Hold = hold;
        }
    }


    /// <summary>
    /// The recap outcome sequence as a function of a feel profile, an outcome context and a time: which
    /// spans it has, and what every renderer shows at any instant inside one. It never drives time: the
    /// game plays it on the real clock through <see cref="RecapPlayback"/>, the preview host scrubs it.
    /// A run outcome fades the HUD, raises the banner until a press, drops it, waits, then raises the
    /// breakdown; an extraction is the card alone. https://github.com/amindell11/astronomical-home/issues/1151
    /// </summary>
    public sealed class RecapOutcomeSequence
    {
        private static readonly RecapPhase[] OutcomePhases =
        {
            RecapPhase.HudFade, RecapPhase.BannerIn, RecapPhase.BannerHold, RecapPhase.BannerOut,
            RecapPhase.Gap, RecapPhase.BreakdownIn, RecapPhase.Breakdown,
        };

        private static readonly RecapPhase[] ExtractionPhases = { RecapPhase.Breakdown };

        private readonly RecapFeelProfile feel;
        private readonly RecapPhase[] phases;

        public RecapOutcomeContext Context { get; }

        public int SpanCount => phases.Length;

        public RecapOutcomeSequence(RecapFeelProfile feel, RecapOutcomeContext context)
        {
            this.feel = feel ? feel : throw new ArgumentNullException(nameof(feel));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            phases = context.Outcome == RecapOutcome.Extraction ? ExtractionPhases : OutcomePhases;
        }

        /// <summary>Reads the profile on every call, so a live edit moves the next frame.</summary>
        public RecapSpan Span(int index)
        {
            var phase = phases[index];
            return phase switch
            {
                RecapPhase.HudFade => new RecapSpan(phase, Context.Outcome == RecapOutcome.Defeat
                    ? Mathf.Max(feel.HudFadeSeconds, feel.DeathRevealDelaySeconds) : feel.HudFadeSeconds,
                    RecapHold.None),
                RecapPhase.BannerIn => new RecapSpan(phase, feel.BannerInSeconds, RecapHold.None),
                RecapPhase.BannerHold => new RecapSpan(phase, 0f, RecapHold.Press),
                RecapPhase.BannerOut => new RecapSpan(phase, feel.BannerOutSeconds, RecapHold.None),
                RecapPhase.Gap => new RecapSpan(phase, feel.GapSeconds, RecapHold.None),
                RecapPhase.BreakdownIn => new RecapSpan(phase, feel.BreakdownInSeconds, RecapHold.Release),
                RecapPhase.Breakdown => new RecapSpan(phase, 0f, RecapHold.Continue),
                _ => throw new ArgumentOutOfRangeException(nameof(index), phase, "Unknown recap phase."),
            };
        }

        /// <summary>Seconds past the span's timed part clamp: the hold keeps the end state.</summary>
        public RecapState StateAt(int index, float seconds)
        {
            var span = Span(index);
            var t = Progress(seconds, span.Seconds);
            if (Context.Outcome == RecapOutcome.Extraction)
                return new RecapState(span.Phase, 0f, 1f, false, true, 0f, false, 1f, true);

            var hud = span.Phase == RecapPhase.HudFade
                ? 1f - Smoother(Progress(seconds, feel.HudFadeSeconds)) : 0f;
            var frozen = Context.Outcome == RecapOutcome.Victory;
            return span.Phase switch
            {
                RecapPhase.HudFade => new RecapState(span.Phase, hud, hud, true, frozen, 0f, false, 0f, false),
                RecapPhase.BannerIn => new RecapState(span.Phase, 0f, 0f, true, frozen, Mathf.SmoothStep(0f, 1f, t), false, 0f, false),
                RecapPhase.BannerHold => new RecapState(span.Phase, 0f, 0f, true, frozen, 1f, true, 0f, false),
                RecapPhase.BannerOut => new RecapState(span.Phase, 0f, 0f, true, frozen, 1f - Mathf.SmoothStep(0f, 1f, t), false, 0f, false),
                RecapPhase.Gap => new RecapState(span.Phase, 0f, 0f, true, frozen, 0f, false, 0f, false),
                RecapPhase.BreakdownIn => new RecapState(span.Phase, 0f, 0f, true, frozen, 0f, false, Mathf.SmoothStep(0f, 1f, t), false),
                RecapPhase.Breakdown => new RecapState(span.Phase, 0f, 0f, true, frozen, 0f, false, 1f, true),
                _ => throw new ArgumentOutOfRangeException(nameof(index), span.Phase, "Unknown recap phase."),
            };
        }

        private static float Progress(float seconds, float duration) =>
            duration <= 0f ? 1f : Mathf.Clamp01(seconds / duration);

        private static float Smoother(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
