using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// Where the hosted subject is on its timeline and how it moves: play, pause, scrub, loop, speed.
    /// An input hold lasts <see cref="HoldSeconds"/> here, since no player presses anything.
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

        public void Tick(float unscaledDelta, IPreviewSubject subject)
        {
            if (Playing) Advance(unscaledDelta * Speed, subject);
            subject.Show(Span, Seconds);
        }

        private void Advance(float delta, IPreviewSubject subject)
        {
            Seconds += delta;
            var length = Length(subject.Span(Span));
            if (Seconds < length || subject.Busy) return;
            if (Span + 1 < subject.SpanCount) Scrub(Span + 1, 0f);
            else if (Loop) Restart();
            else
            {
                Seconds = length;
                Playing = false;
            }
        }
    }
}
