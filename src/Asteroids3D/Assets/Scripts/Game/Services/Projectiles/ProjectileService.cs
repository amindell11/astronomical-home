using System;
using System.Collections.Generic;
using Combat.Projectiles;
using Game.Presentation;
using UnityEngine;

namespace Game.Services.Projectiles
{
    public class ProjectileService : IProjectileService
    {
        private sealed class Entry
        {
            public Action ReturnToPool;
            public Action OnReturned;
        }

        private readonly Transform liveRoot;
        private readonly bool presentationEnabled;
        private readonly Dictionary<MonoBehaviour, Entry> live = new();
        // Pooled instances are process-wide and cross sessions, so presentation is re-applied on every
        // checkout — a headless session may hand a darkened instance back to a presenting one.
        private readonly Dictionary<MonoBehaviour, PresentationApplier.Parts> presentationParts = new();

        /// <summary>Tracked instances are parented under <paramref name="liveRoot"/> (a non-moving context root: session, arena, or fixture host), so destroying the context destroys its in-flight transients — debris cannot outlive its owner.</summary>
        public ProjectileService(Transform liveRoot, bool presentationEnabled = true)
        {
            this.liveRoot = liveRoot ? liveRoot : throw new ArgumentNullException(nameof(liveRoot));
            this.presentationEnabled = presentationEnabled;
        }

        public void Register(MonoBehaviour instance, Action returnToPool)
        {
            if (!instance) return;
            if (returnToPool == null) throw new ArgumentNullException(nameof(returnToPool));

            if (live.TryGetValue(instance, out var existing))
            {
                // Unreachable unless a pool handed out an instance that never raised its return event — surface the corruption instead of absorbing it.
                Debug.LogError($"ProjectileService: {instance.name} registered while already live — pool return path skipped its event?", instance);
                existing.ReturnToPool = returnToPool;
                return;
            }

            var entry = new Entry { ReturnToPool = returnToPool };
            entry.OnReturned = () => Deregister(instance, entry);
            live.Add(instance, entry);
            instance.transform.SetParent(liveRoot, true);
            ApplyPresentation(instance);
            SubscribeReturn(instance, entry.OnReturned);
            if (instance is ITransientSpawner spawner)
                spawner.Spawned += Register;
        }

        public void ReturnAllToPool()
        {
            // Local snapshot: return events mutate the set mid-flush, and a nested flush must not corrupt this one's iteration.
            var snapshot = new List<KeyValuePair<MonoBehaviour, Entry>>(live);
            foreach (var (instance, entry) in snapshot)
            {
                if (!live.TryGetValue(instance, out var current) || current != entry) continue;
                if (!instance)
                {
                    Deregister(instance, entry);
                    continue;
                }
                entry.ReturnToPool();
            }
        }

        public int ActiveCount
        {
            get
            {
                PruneCorpses();
                return live.Count;
            }
        }

        public void ForEachLive(Action<MonoBehaviour> visit)
        {
            if (visit == null) return;
            PruneCorpses();
            foreach (var instance in live.Keys)
                visit(instance);
        }

        private void ApplyPresentation(MonoBehaviour instance)
        {
            if (!presentationParts.TryGetValue(instance, out var parts))
            {
                parts = PresentationApplier.Capture(instance.gameObject);
                presentationParts[instance] = parts;
            }
            PresentationApplier.Apply(parts, presentationEnabled);
        }

        private void Deregister(MonoBehaviour instance, Entry entry)
        {
            if (!live.TryGetValue(instance, out var current) || current != entry) return;
            live.Remove(instance);
            UnsubscribeReturn(instance, entry.OnReturned);
            if (instance is ITransientSpawner spawner)
                spawner.Spawned -= Register;
        }

        // The service knows the domain's return events; registrants never learn the service exists.
        private static void SubscribeReturn(MonoBehaviour instance, Action onReturned)
        {
            switch (instance)
            {
                case ProjectileBase projectile: projectile.ReturnedToPool += onReturned; break;
                case ConcussionWave wave: wave.Released += onReturned; break;
            }
        }

        private static void UnsubscribeReturn(MonoBehaviour instance, Action onReturned)
        {
            switch (instance)
            {
                case ProjectileBase projectile: projectile.ReturnedToPool -= onReturned; break;
                case ConcussionWave wave: wave.Released -= onReturned; break;
            }
        }

        // Pooled instances can be destroyed out from under their registration (context teardown) without raising a return event; skip and drop those corpses.
        private void PruneCorpses()
        {
            List<KeyValuePair<MonoBehaviour, Entry>> corpses = null;
            foreach (var pair in live)
            {
                if (pair.Key) continue;
                corpses ??= new List<KeyValuePair<MonoBehaviour, Entry>>();
                corpses.Add(pair);
            }
            if (corpses == null) return;
            foreach (var (instance, entry) in corpses)
                Deregister(instance, entry);
        }
    }
}
