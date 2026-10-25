using System;
using System.Collections.Generic;
using Game.Travel;
using UnityEngine;

namespace Game.Preview
{
    public sealed class SectorJumpPreviewSubject : IPreviewSubject
    {
        private static readonly string[] Names =
        {
            "Blue to copper, diagonal", "Blue to copper, upward", "Blue to copper, left",
            "Copper to blue, diagonal", "Blue to copper, alternate stars"
        };
        private readonly SectorJumpPresentation presentation;
        private readonly Action release;
        private SectorJumpProfile profile;
        private SectorJumpSequence sequence;
        private int revision = -1;

        public SectorJumpPreviewSubject(SectorJumpProfile profile, SectorJumpPresentation presentation, Action release)
        {
            this.presentation = presentation;
            this.release = release;
            Knobs = profile;
        }

        public string Name => "Sector jump";
        public IReadOnlyList<string> Fixtures => Names;
        public int Fixture { get; private set; }
        public int SpanCount => 1;
        public bool Busy => false;
        public SectorJumpState State { get; private set; }

        public ScriptableObject Knobs
        {
            get => profile;
            set
            {
                profile = value as SectorJumpProfile;
                if (!profile) throw new ArgumentException("Sector jump requires a SectorJumpProfile.", nameof(value));
                revision = -1;
            }
        }

        public PreviewSpan Span(int index) => new("Jump", profile.Duration, false);

        public void SelectFixture(int index)
        {
            if (index < 0 || index >= Names.Length) throw new ArgumentOutOfRangeException(nameof(index));
            Fixture = index;
        }

        public void Show(int span, float seconds)
        {
            if (revision != profile.Revision)
            {
                sequence = new SectorJumpSequence(profile);
                revision = profile.Revision;
            }
            var heading = Fixture == 1 ? 0f : Fixture == 2 ? 90f : -35f;
            State = sequence.StateAt(seconds, heading);
            presentation.Apply(profile, State, Fixture == 4 ? 417 : 1099, Fixture == 3);
        }

        public void Dispose() => release();
    }
}
