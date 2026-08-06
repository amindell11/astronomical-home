using AI;
using AI.Context;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The curriculum floor: a killable airframe pinned to a zero-velocity reference — no motion goal, no aim, no fire.</summary>
    public class DummyChooser : IIntentChooser
    {
        public ActIntent Decide(AIContext ctx, float dt) =>
            ctx?.Self == null
                ? ActIntent.None
                : new ActIntent
                {
                    isValid = true,
                    velocityReference = Vector2.zero,
                };
    }
}
