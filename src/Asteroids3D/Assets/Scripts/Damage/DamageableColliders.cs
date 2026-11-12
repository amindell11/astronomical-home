using System;
using System.Collections.Generic;
using UnityEngine;

namespace Damage
{
    /// <summary>
    /// Identity map from a collider to the damageable that owns it, filled in Awake.
    /// A collider belongs to one entity in any arena, so this is not arena state.
    /// Registering a collider that already has an owner throws.
    /// </summary>
    public static class DamageableColliders
    {
        private static readonly Dictionary<int, IDamageable> ownersByCollider = new();
        private static readonly Dictionary<int, int[]> collidersByOwner = new();

        public static void Register(IDamageable owner, Collider[] colliders)
        {
            var component = (Component)owner;
            var ownerId = component.GetInstanceID();
            if (collidersByOwner.ContainsKey(ownerId))
                throw new InvalidOperationException($"{Label(component)} is already registered.");

            var ids = new int[colliders.Length];
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                var id = collider.GetInstanceID();
                if (ownersByCollider.TryGetValue(id, out var existing))
                {
                    for (var j = 0; j < i; j++)
                        ownersByCollider.Remove(ids[j]);
                    throw new InvalidOperationException(
                        $"Collider '{collider.name}' is already registered to {Label((Component)existing)}; {Label(component)} cannot register it.");
                }

                ownersByCollider.Add(id, owner);
                ids[i] = id;
            }

            collidersByOwner.Add(ownerId, ids);
        }

        public static void Unregister(IDamageable owner)
        {
            var ownerId = ((Component)owner).GetInstanceID();
            if (!collidersByOwner.TryGetValue(ownerId, out var ids)) return;
            collidersByOwner.Remove(ownerId);
            for (var i = 0; i < ids.Length; i++)
                ownersByCollider.Remove(ids[i]);
        }

        public static bool TryGet(Collider collider, out IDamageable owner)
        {
            if (!collider)
            {
                owner = null;
                return false;
            }

            return ownersByCollider.TryGetValue(collider.GetInstanceID(), out owner);
        }

        private static string Label(Component component) =>
            $"{component.GetType().Name} on '{component.name}'";
    }
}
