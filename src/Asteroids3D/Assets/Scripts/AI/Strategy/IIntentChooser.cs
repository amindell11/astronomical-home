using System.Collections.Generic;
using AI.Context;
using AI.States;

namespace AI
{
    /// <summary>
    /// The swappable decision policy: maps the per-tick world model to an action
    /// (<see cref="NavigationIntent"/>). It decides; it does not actuate — the hosting
    /// <see cref="Brain"/> applies the returned intent to the Navigator and Gunner. A policy
    /// (the hand-authored <see cref="Utility.UtilityChooser"/> today, an RL policy later)
    /// plugs in by implementing this one method.
    /// </summary>
    public interface IIntentChooser
    {
        /// <summary>
        /// Decide the action for this tick. Return <see cref="NavigationIntent.None"/>
        /// to idle/reset (e.g. no decision available, or mid-transition).
        /// </summary>
        NavigationIntent Decide(AIContext ctx, float dt);
    }

    /// <summary>
    /// An <see cref="IIntentChooser"/> that picks among a fixed set of discrete behaviors
    /// (<see cref="AIState"/>). The commander builds the option set and hands it over here.
    /// A continuous-control policy (e.g. raw RL) implements <see cref="IIntentChooser"/> only.
    /// </summary>
    public interface IStateChooser : IIntentChooser
    {
        void Initialize(IReadOnlyList<AIState> states);
    }
}
