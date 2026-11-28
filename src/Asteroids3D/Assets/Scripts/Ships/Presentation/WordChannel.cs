using System;
using UnityEngine;

namespace Ships.Presentation
{
    public readonly struct Word
    {
        public readonly string Text;
        public readonly Color Color;

        public Word(string text, Color color)
        {
            Text = text;
            Color = color;
        }
    }

    /// <summary>
    /// Short words a ship's hits and effects pop at a world point. Raised on the ship that caused
    /// them, never the one they land on, so the HUD shows the words raised on the player's ship.
    /// </summary>
    public sealed class WordChannel
    {
        public event Action<Word, Vector3> Raised;

        public void Raise(Word word, Vector3 at) => Raised?.Invoke(word, at);
    }
}
