using System;
using System.Collections.Generic;

namespace Game.Sectors
{
    /// <summary>Per-sector boolean signals keyed on port identity: levels via Set, latched events via Latch; fresh instance per sector Setup, frozen when teardown begins.</summary>
    public class SectorEventBus
    {
        private readonly Dictionary<SignalPort, bool> signals = new();
        private readonly HashSet<SignalPort> latched = new();

        public event Action<SignalPort> Changed;

        public bool Frozen { get; private set; }

        public void Freeze() => Frozen = true;

        public bool Get(SignalPort port) => port && signals.TryGetValue(port, out var value) && value;

        public void Set(SignalPort port, bool value)
        {
            if (Frozen || !port) return;
            if (!value && latched.Contains(port)) return;
            if (Get(port) == value) return;
            signals[port] = value;
            Changed?.Invoke(port);
        }

        public void Latch(SignalPort port)
        {
            if (Frozen || !port) return;
            latched.Add(port);
            Set(port, true);
        }
    }
}
