using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    internal sealed class RigidbodyOccupancy
    {
        private readonly Dictionary<Rigidbody, HashSet<Collider>> occupants = new();

        // Unity fires no OnTriggerExit for a collider disabled/destroyed inside the trigger — prune stale colliders on read.
        public bool Contains(Rigidbody body)
        {
            if (!body || !occupants.TryGetValue(body, out var colliders)) return false;
            colliders.RemoveWhere(IsGone);
            if (colliders.Count > 0) return true;
            occupants.Remove(body);
            return false;
        }

        public void Enter(Collider collider)
        {
            var body = collider ? collider.attachedRigidbody : null;
            if (!body) return;
            if (!occupants.TryGetValue(body, out var colliders))
                occupants[body] = colliders = new HashSet<Collider>();
            colliders.Add(collider);
        }

        public void Exit(Collider collider)
        {
            var body = collider ? collider.attachedRigidbody : null;
            if (!body || !occupants.TryGetValue(body, out var colliders)) return;
            colliders.Remove(collider);
            if (colliders.Count == 0) occupants.Remove(body);
        }

        private static bool IsGone(Collider collider) =>
            !collider || !collider.enabled || !collider.gameObject.activeInHierarchy;
    }
}
