#if UNITY_EDITOR
using AI.Context;

namespace AI.States
{
    public partial class AIState
    {
        public void OnDrawGizmos(AIContext ctx)
        {
            if (ctx == null) return;
            goalRunner.OnDrawGizmos(ctx, Profile);
        }
    }
}
#endif
