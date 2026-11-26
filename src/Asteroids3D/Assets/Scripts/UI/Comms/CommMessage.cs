using System;
using UnityEngine;

namespace UI.Comms
{
    public readonly struct CommMessage
    {
        public string Speaker { get; }
        public Color SpeakerColor { get; }
        public string Text { get; }

        public CommMessage(string speaker, Color speakerColor, string text)
        {
            Speaker = speaker ?? throw new ArgumentNullException(nameof(speaker));
            SpeakerColor = speakerColor;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public string Plain => Speaker + ": " + Text;
    }
}
