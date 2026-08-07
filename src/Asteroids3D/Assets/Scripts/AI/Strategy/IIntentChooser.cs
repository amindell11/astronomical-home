using AI.Context;

namespace AI
{
    /// <summary>The swappable decision policy: maps the per-tick world model to an <see cref="ActIntent"/>. It decides, never actuates — the hosting <see cref="Brain"/> applies the intent to the Navigator and Gunner.</summary>
    public interface IIntentChooser
    {
        /// <summary>Decide this tick's action; return <see cref="ActIntent.None"/> to idle/reset (no decision available, or mid-transition).</summary>
        ActIntent Decide(AIContext ctx, float dt);

        /// <summary>Discard accumulated decision state so the next Decide behaves as freshly initialized.</summary>
        void Reset() { }
    }
}
