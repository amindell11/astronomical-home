using System.Collections.Generic;
using Game.Services;
using Ships;

namespace Game.Capture
{
    public enum GizmoCaptureProfile
    {
        None,
        Steering,
        Combat,
        Everything,
    }

    public interface IEpisodeCapture
    {
        /// <summary>Films <paramref name="subjects"/> — selected for gizmo drawing, framed by the capture camera — until End. The first subject anchors Game View focus.</summary>
        void Begin(CaptureConfig config, GizmoCaptureProfile profile, IReadOnlyList<Ship> subjects,
            IProjectileService projectiles);
        void Step();
        void End();

        /// <summary>Where the frames of the episode most recently begun are written. Null before the first Begin.</summary>
        string FrameDir { get; }
    }
}
