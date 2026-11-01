namespace UI.Projection
{
    public sealed class CanopyStateBlend
    {
        private readonly CanopyProjectionProfile profile;
        private CanopySettings from;
        private float elapsed;
        private float duration;

        public CanopyState State { get; private set; }
        public CanopySettings Settings { get; private set; }

        public CanopyStateBlend(CanopyProjectionProfile profile)
        {
            this.profile = profile;
            Settings = profile.baseSettings;
        }

        public void Tick(CanopyState state, float deltaTime)
        {
            if (state != State)
            {
                from = Settings;
                duration = state > State ? profile.OverrideFor(state).blendInSeconds
                    : profile.OverrideFor(State).blendOutSeconds;
                elapsed = 0f;
                State = state;
            }
            elapsed += deltaTime;
            var target = profile.Resolve(State);
            Settings = duration <= 0f ? target : CanopySettings.Lerp(from, target, elapsed / duration);
        }

        public static CanopyState Select(bool lowHull, bool bigMoment) => bigMoment ? CanopyState.BigMoment
            : lowHull ? CanopyState.LowHull : CanopyState.Base;
    }

    public enum CanopyState { Base, LowHull, BigMoment }
}
