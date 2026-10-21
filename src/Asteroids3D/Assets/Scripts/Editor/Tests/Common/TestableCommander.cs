using AI;

namespace Tests.Common
{
    public sealed class TestableCommander : AICommander
    {
        public void CallAwake() => Awake();
        public void Step() => FixedUpdate();
    }
}
