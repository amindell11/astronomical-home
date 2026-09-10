#if UNITY_EDITOR
using AI;
using AI.Context;
using AI.Strategy;
using Ships;

namespace Tests.PlayMode.Scenarios
{
    public sealed class TerminalFieldChaseBrain : Brain
    {
        private Ship target;
        public void Initialize(Ship target) => this.target = target;

        public override BrainDecision? Decide(AIContext context)
        {
            if (!target || !target.gameObject.activeInHierarchy) return null;
            return new BrainDecision(NavObjective.Anchored(target.Id)
                .Facing(0f, 1f).Position(0f, 0f, 6f, 1f).Velocity(8f, 0f, 0.5f).Field(1f), false, false);
        }
    }
}
#endif
