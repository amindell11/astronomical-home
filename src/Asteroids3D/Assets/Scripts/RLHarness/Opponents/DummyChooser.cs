using AI;
using AI.Context;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The curriculum floor: a killable airframe pinned to a zero-velocity reference — no motion goal, no aim, no fire.</summary>
    public class DummyChooser : IIntentChooser
    {
        public BrainDecision? Decide(AIContext ctx, float dt) =>
            ctx?.Self == null
                ? null
                : new BrainDecision(NavObjective.Planar(Vector2.zero));
    }
}
