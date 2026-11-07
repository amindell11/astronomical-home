using System;
using System.Collections;

namespace Capture.Recording
{
    /// <summary>
    /// Preparation renders the supplied view without writing frames. Start activates acquisition;
    /// Finish returns only completed image/metadata pairs. Dispose aborts and restores capture settings.
    /// The caller owns simulation and pacing, including rendering any terminal state before Finish.
    /// </summary>
    public abstract class CaptureRecording : IDisposable
    {
        public abstract IEnumerator Ready { get; }
        public abstract void Start();
        public abstract CaptureResult Finish();
        public abstract void Dispose();
    }
}
