using System.Collections.Generic;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>One stretch of a subject's timeline: a timed part, then for an input hold a wait the transport paces.</summary>
    public readonly struct PreviewSpan
    {
        public string Name { get; }
        public float Seconds { get; }
        public bool WaitsForInput { get; }

        public PreviewSpan(string name, float seconds, bool waitsForInput)
        {
            Name = name;
            Seconds = seconds;
            WaitsForInput = waitsForInput;
        }
    }

    /// <summary>
    /// A presentation module with a chosen context, hosted by <see cref="PreviewHost"/>: it names its
    /// fixtures and spans, shows any instant of its timeline on the real renderers, and exposes the
    /// feel profile the control panel edits. The transport decides which instant; the subject never
    /// drives time.
    /// </summary>
    public interface IPreviewSubject
    {
        string Name { get; }
        IReadOnlyList<string> Fixtures { get; }
        int Fixture { get; }
        void SelectFixture(int index);
        int SpanCount { get; }
        PreviewSpan Span(int index);

        /// <summary>True while the shown span's staged moment still plays; the transport stays until it ends.</summary>
        bool Busy { get; }

        void Show(int span, float seconds);
        ScriptableObject Knobs { get; }
    }
}
