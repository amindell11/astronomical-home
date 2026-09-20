using System;
using UnityEngine;

namespace Capture
{
    /// <summary>Locked frame pacing for capture runs: a whole number of fixed steps per rendered frame, so recorded clips are deterministic and assemble to real-time playback whatever the wall-clock speed.</summary>
    public static class CapturePacing
    {
        public static IDisposable Locked(int fixedStepsPerFrame = 1) => new Scope(fixedStepsPerFrame);

        private sealed class Scope : IDisposable
        {
            private readonly float savedTimeScale = Time.timeScale;
            private readonly float savedCaptureDelta = Time.captureDeltaTime;

            internal Scope(int fixedStepsPerFrame)
            {
                Time.timeScale = 1f;
                Time.captureDeltaTime = Time.fixedDeltaTime * fixedStepsPerFrame;
            }

            public void Dispose()
            {
                Time.timeScale = savedTimeScale;
                Time.captureDeltaTime = savedCaptureDelta;
            }
        }
    }
}
