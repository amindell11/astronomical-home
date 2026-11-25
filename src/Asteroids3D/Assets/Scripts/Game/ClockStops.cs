using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Stops of the simulation clock: time stands still while any hold is open, and the scale it ran at
    /// comes back when the last hold closes, so a pause over a stop cannot restart time early.
    /// </summary>
    internal sealed class ClockStops
    {
        private readonly List<Token> holds = new();
        private float runningScale;

        public bool Stopped => holds.Count > 0;

        public IDisposable Hold()
        {
            if (holds.Count == 0)
            {
                runningScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            var token = new Token(this);
            holds.Add(token);
            return token;
        }

        public void ReleaseAll()
        {
            if (holds.Count == 0) return;
            holds.Clear();
            Time.timeScale = runningScale;
        }

        private void Release(Token hold)
        {
            if (!holds.Remove(hold) || holds.Count > 0) return;
            Time.timeScale = runningScale;
        }

        private sealed class Token : IDisposable
        {
            private ClockStops owner;

            public Token(ClockStops owner) => this.owner = owner;

            public void Dispose()
            {
                var current = owner;
                owner = null;
                current?.Release(this);
            }
        }
    }
}
