using AI;
using AI.Context;
using AI.States;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The curriculum floor: a killable airframe pinned to a zero-velocity reference — no motion goal, no aim, no fire.</summary>
    public class DummyChooser : IIntentChooser
    {
        public NavigationIntent Decide(AIContext ctx, float dt) =>
            ctx?.Self == null
                ? NavigationIntent.None
                : new NavigationIntent
                {
                    isValid = true,
                    velocityReference = Vector2.zero,
                };
    }
}
