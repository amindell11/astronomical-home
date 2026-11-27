using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Holds on the simulation clock: a stop stands time still, a slow runs it at its own scale, and the
    /// slowest open hold wins. The scale the clock ran at comes back when the last hold closes, so a pause
    /// over a stop or a slow cannot restart time early.
    /// </summary>
    internal sealed class ClockStops
    {
        private readonly List<Token> holds = new();
        private float runningScale;

        public bool Stopped
        {
            get
            {
                foreach (var hold in holds)
                    if (hold.Scale == 0f) return true;
                return false;
            }
        }

        public IDisposable Hold() => Open(0f);

        /// <summary>Runs the clock at <paramref name="scale"/> of its running speed, changeable while held.</summary>
        public ClockSlow Slow(float scale) => Open(scale);

        public void ReleaseAll()
        {
            if (holds.Count == 0) return;
            holds.Clear();
            Time.timeScale = runningScale;
        }

        private Token Open(float scale)
        {
            if (holds.Count == 0) runningScale = Time.timeScale;
            var token = new Token(this, scale);
            holds.Add(token);
            Apply();
            return token;
        }

        private void Apply()
        {
            var scale = 1f;
            foreach (var hold in holds)
                scale = Mathf.Min(scale, hold.Scale);
            Time.timeScale = runningScale * scale;
        }

        private void Release(Token hold)
        {
            if (!holds.Remove(hold)) return;
            if (holds.Count > 0) Apply();
            else Time.timeScale = runningScale;
        }

        private void Rescale(Token hold)
        {
            if (holds.Contains(hold)) Apply();
        }

        private sealed class Token : ClockSlow
        {
            private ClockStops owner;
            private float scale;

            public Token(ClockStops owner, float scale)
            {
                this.owner = owner;
                this.scale = Mathf.Clamp01(scale);
            }

            public override float Scale
            {
                get => scale;
                set
                {
                    scale = Mathf.Clamp01(value);
                    owner?.Rescale(this);
                }
            }

            public override void Dispose()
            {
                var current = owner;
                owner = null;
                current?.Release(this);
            }
        }
    }

    internal abstract class ClockSlow : IDisposable
    {
        public abstract float Scale { get; set; }
        public abstract void Dispose();
    }
}
