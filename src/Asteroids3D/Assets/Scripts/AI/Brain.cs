using AI.Context;
using UnityEngine;
using AI.Strategy;

namespace AI
{
    /// <summary>The swappable decision unit: maps the per-tick world model to a <see cref="BrainDecision"/>. It decides, never actuates — the hosting <see cref="AICommander"/> routes each lane to its own consumer. Installed through <see cref="AICommander.InstallBrain{T}"/> or authored on the pilot prefab.</summary>
    public abstract class Brain : MonoBehaviour
    {
        /// <summary>Decide this tick's action; null when no decision is available (mid-transition, or no live target).</summary>
        public abstract BrainDecision? Decide(AIContext ctx);

        /// <summary>Discard accumulated decision state so the next Decide behaves as freshly initialized.</summary>
        public virtual void ResetState() { }
    }
}
