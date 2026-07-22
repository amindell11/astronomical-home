using AI.Context;
using AI.States;

namespace AI
{
    /// <summary>The swappable decision policy: maps the per-tick world model to a <see cref="NavigationIntent"/>. It decides, never actuates — the hosting <see cref="Brain"/> applies the intent to the Navigator and Gunner.</summary>
    public interface IIntentChooser
    {
        /// <summary>Decide this tick's action; return <see cref="NavigationIntent.None"/> to idle/reset (no decision available, or mid-transition).</summary>
        NavigationIntent Decide(AIContext ctx, float dt);

        /// <summary>Discard accumulated decision state so the next Decide behaves as freshly initialized.</summary>
        void Reset() { }
    }
}
