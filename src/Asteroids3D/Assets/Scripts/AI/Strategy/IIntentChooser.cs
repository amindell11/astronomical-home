using AI.Context;
using AI.States;
using Movement.MPC;
using Ships.Command;

namespace AI
{
    /// <summary>The swappable decision policy: maps the per-tick world model to a <see cref="NavigationIntent"/>. It decides, never actuates — the hosting <see cref="Brain"/> applies the intent to the Navigator and Gunner.</summary>
    public interface IIntentChooser
    {
        /// <summary>Decide this tick's action; return <see cref="NavigationIntent.None"/> to idle/reset (no decision available, or mid-transition).</summary>
        NavigationIntent Decide(AIContext ctx, float dt);

        /// <summary>Discard accumulated decision state so the next Decide behaves as freshly initialized (stateless policies need not override).</summary>
        void Reset() { }
    }

    /// <summary>An <see cref="IIntentChooser"/> that owns an authored set of discrete behaviors: the host binds actuators and seed, and the chooser builds its own option set from them. A continuous-control policy implements <see cref="IIntentChooser"/> only.</summary>
    public interface IStateChooser : IIntentChooser
    {
        void Initialize(Navigator navigator, Gunner gunner, SeedScope strategyScope);
    }
}
