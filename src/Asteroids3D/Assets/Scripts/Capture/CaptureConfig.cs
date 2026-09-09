using System;

namespace Game.Capture
{
    /// <summary>
    /// Runtime-safe mirror of the editor-only Gizmo View scope: a scenario author picks which ships a
    /// capture's gizmos draw over, without the runtime capture assembly referencing the editor enum.
    /// The capture transaction maps this onto <c>Game.Diagnostics.GizmoScope</c>.
    /// </summary>
    public enum CaptureGizmoScope
    {
        All,
        Selected,
        Team,
    }

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
        /// <summary>Gizmo View scope the capture drives for its run. Default All films every ship's gizmos.</summary>
        public CaptureGizmoScope gizmoScope = CaptureGizmoScope.All;
        /// <summary>Team number scoped when <see cref="gizmoScope"/> is Team; ignored otherwise.</summary>
        public int gizmoScopeTeam;
    }
}
