using System;
using UnityEngine;

namespace Game.Capture
{
    /// <summary>Locked frame pacing for capture runs: 1 fixed step per rendered frame, so recorded clips are deterministic and assemble to real-time playback whatever the wall-clock speed.</summary>
    public static class CapturePacing
    {
        public static IDisposable Locked() => new Scope();

        private sealed class Scope : IDisposable
        {
            private readonly float savedTimeScale = Time.timeScale;
            private readonly float savedCaptureDelta = Time.captureDeltaTime;

            internal Scope()
            {
                Time.timeScale = 1f;
                Time.captureDeltaTime = Time.fixedDeltaTime;
            }

            public void Dispose()
            {
                Time.timeScale = savedTimeScale;
                Time.captureDeltaTime = savedCaptureDelta;
            }
        }
    }
}
