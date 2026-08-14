using System;

namespace Game.Capture
{
    [Serializable]
    public sealed class CaptureConfig
    {
        public string outputRoot = "results/capture";
        public string clipName = "clip";
        /// <summary>Shared across clips of one run; null → stamped when the episode begins.</summary>
        public string runStamp;
        public int width = 960;
        public int height = 540;
        /// <summary>Capture cadence in fixed steps. 5 → 0.1 s of sim per frame at the default 0.02 fixed dt, real-time playback at 10 fps.</summary>
        public int everyFixedSteps = 5;
        public float minHalfHeight = 22f;
        public float padding = 12f;
    }
}
