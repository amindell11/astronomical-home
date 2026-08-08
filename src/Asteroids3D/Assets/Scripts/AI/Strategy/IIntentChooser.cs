using AI.Context;

namespace AI
{
    /// <summary>The swappable decision policy: maps the per-tick world model to a <see cref="BrainDecision"/>. It decides, never actuates — the hosting <see cref="AICommander"/> routes each lane to its own consumer.</summary>
    public interface IIntentChooser
    {
        /// <summary>Decide this tick's action; null when no decision is available (mid-transition, or no live target).</summary>
        BrainDecision? Decide(AIContext ctx, float dt);

        /// <summary>Discard accumulated decision state so the next Decide behaves as freshly initialized.</summary>
        void Reset() { }
    }
}
