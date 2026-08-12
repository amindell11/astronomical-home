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
        void Begin(CaptureConfig config, GizmoCaptureProfile profile, Ship a, Ship b,
            IProjectileService projectiles);
        void Step();
        void End();
    }
}
