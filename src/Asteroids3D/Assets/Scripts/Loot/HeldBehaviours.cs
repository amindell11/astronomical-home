using System;
using System.Collections.Generic;

namespace Loot
{
    /// <summary>
    /// The behaviour mods in force on one part. The part's owner replaces the set on every change to
    /// the run's mods; consumers ask <see cref="Holds"/> and observe <see cref="Changed"/>, which
    /// fires only when the set itself changes.
    /// </summary>
    public sealed class HeldBehaviours
    {
        private readonly HashSet<BehaviourMod> held = new();

        public event Action Changed;

        public bool Holds(BehaviourMod key)
        {
            if (!key) throw new ArgumentNullException(nameof(key));
            return held.Contains(key);
        }

        public void Set(IEnumerable<BehaviourMod> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (held.SetEquals(keys)) return;
            held.Clear();
            held.UnionWith(keys);
            Changed?.Invoke();
        }
    }
}
