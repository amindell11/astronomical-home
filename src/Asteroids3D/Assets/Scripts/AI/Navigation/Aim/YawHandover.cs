using AI.Navigation.MPC;
using AI.Playtest;

namespace AI.Navigation.Aim
{
    /// <summary>Decides each step whether the aim servo or the MPC owns yaw. The AIM slot's authority latches the servo in with hysteresis rather than blending torques, and an obstacle threat hands yaw back to the MPC until the threat has stayed clear for the veto hold.</summary>
    public sealed class YawHandover
    {
        private readonly MpcSettings settings;
        private bool latched;
        private float sinceThreat = float.PositiveInfinity;

        public YawHandover(MpcSettings settings) => this.settings = settings;

        private bool Vetoed => sinceThreat < settings.aimServoVetoHold;

        public bool ServoOwnsYaw(in AimReference reference) => LethalityPresets.ServoOn && reference.live && latched && !Vetoed;

        public void UpdateAuthority(float aimAuthority) =>
            latched = aimAuthority >= (latched ? settings.aimServoReleaseWeight : settings.aimServoEngageWeight);

        public void ObserveThreat(bool obstacleThreat, float dt) =>
            sinceThreat = obstacleThreat ? 0f : sinceThreat + dt;
    }
}
