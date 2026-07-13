using System;
using System.Collections.Generic;

namespace Game.Sectors
{
    /// <summary>Per-sector named boolean signals: levels via Set, latched events via Latch; fresh instance per sector Setup.</summary>
    public class SectorEventBus
    {
        private readonly Dictionary<string, bool> signals = new();
        private readonly HashSet<string> latched = new();

        public event Action<string> Changed;

        public bool Get(string token) => signals.TryGetValue(token, out var value) && value;

        public void Set(string token, bool value)
        {
            if (!value && latched.Contains(token)) return;
            if (Get(token) == value) return;
            signals[token] = value;
            Changed?.Invoke(token);
        }

        public void Latch(string token)
        {
            latched.Add(token);
            Set(token, true);
        }
    }
}
