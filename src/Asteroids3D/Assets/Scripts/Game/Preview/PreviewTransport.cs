using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Where the hosted subject is on its timeline and how it moves: play, pause, scrub, loop, speed.
    /// An input hold lasts <see cref="HoldSeconds"/> here, since no player presses anything. Only a
    /// subject that scrubs loops by rewinding; one that doesn't is re-staged by the host instead.
    /// </summary>
    public sealed class PreviewTransport
    {
        public int Span { get; private set; }
        public float Seconds { get; private set; }
        public bool Playing { get; set; }
        public bool Loop { get; set; }
        public float Speed { get; set; } = 1f;
        public float HoldSeconds { get; set; } = 1.5f;

        public void Scrub(int span, float seconds)
        {
            Span = Mathf.Max(0, span);
            Seconds = Mathf.Max(0f, seconds);
        }

        public void Restart() => Scrub(0, 0f);

        public float Length(PreviewSpan span) => span.Seconds + (span.WaitsForInput ? HoldSeconds : 0f);

        // Shown before advancing, so a span's staged moment starts before speed can skip past it.
        // True when a looping subject that can't rewind reached its end.
        public bool Tick(float unscaledDelta, IPreviewSubject subject)
        {
            subject.Show(Span, Seconds, HoldSeconds);
            return Playing && Advance(unscaledDelta * Speed, subject);
        }

        private bool Advance(float delta, IPreviewSubject subject)
        {
            Seconds += delta;
            var length = Length(subject.Span(Span));
            if (Seconds < length) return false;
            if (Span + 1 < subject.SpanCount) Scrub(Span + 1, 0f);
            else if (Loop && subject.Scrubs) Restart();
            else if (Loop) return true;
            else
            {
                Seconds = length;
                Playing = false;
            }
            return false;
        }
    }
}
