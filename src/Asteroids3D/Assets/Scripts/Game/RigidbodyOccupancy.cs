using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>Per-rigidbody trigger occupancy: counts collider enters/exits so compound colliders on one body read as a single in/out level.</summary>
    public class RigidbodyOccupancy
    {
        private readonly Dictionary<Rigidbody, int> counts = new();

        public bool Contains(Rigidbody body) => body && counts.ContainsKey(body);

        public void Enter(Rigidbody body)
        {
            if (!body) return;
            counts.TryGetValue(body, out var count);
            counts[body] = count + 1;
        }

        public void Exit(Rigidbody body)
        {
            if (!body || !counts.TryGetValue(body, out var count)) return;
            if (count <= 1) counts.Remove(body);
            else counts[body] = count - 1;
        }
    }
}
