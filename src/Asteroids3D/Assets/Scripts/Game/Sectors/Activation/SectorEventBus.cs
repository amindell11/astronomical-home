using System;
using System.Collections.Generic;

namespace Game.Sectors
{
    /// <summary>Per-sector boolean signals keyed on (publisher, output) identity: levels via Set, latched events via Latch; fresh instance per sector Setup, frozen when teardown begins.</summary>
    public class SectorEventBus
    {
        private readonly Dictionary<SignalRef, bool> signals = new();
        private readonly HashSet<SignalRef> latched = new();

        public event Action<SignalRef> Changed;

        public bool Frozen { get; private set; }

        public void Freeze() => Frozen = true;

        public bool Get(SignalRef signal) =>
            signal.IsAssigned && signals.TryGetValue(signal, out var value) && value;

        public void Set(SignalRef signal, bool value)
        {
            if (Frozen || !signal.IsAssigned) return;
            if (!value && latched.Contains(signal)) return;
            if (Get(signal) == value) return;
            signals[signal] = value;
            Changed?.Invoke(signal);
        }

        public void Latch(SignalRef signal)
        {
            if (Frozen || !signal.IsAssigned) return;
            latched.Add(signal);
            Set(signal, true);
        }
    }
}
