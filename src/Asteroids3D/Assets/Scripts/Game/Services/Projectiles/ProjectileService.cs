using System;
using System.Collections.Generic;
using Combat.Projectile;
using UnityEngine;

namespace Game.Services
{
    public class ProjectileService : IProjectileService
    {
        private sealed class Entry
        {
            public Action ReturnToPool;
            public Action OnReturned;
        }

        private readonly Dictionary<MonoBehaviour, Entry> live = new();
        private readonly List<KeyValuePair<MonoBehaviour, Entry>> flushSnapshot = new();

        public void Register(MonoBehaviour instance, Action returnToPool)
        {
            if (!instance) return;
            if (returnToPool == null) throw new ArgumentNullException(nameof(returnToPool));

            if (live.TryGetValue(instance, out var existing))
            {
                existing.ReturnToPool = returnToPool;
                return;
            }

            var entry = new Entry { ReturnToPool = returnToPool };
            entry.OnReturned = () => Deregister(instance, entry);
            live.Add(instance, entry);
            SubscribeReturn(instance, entry.OnReturned);
            if (instance is ITransientSpawner spawner)
                spawner.Spawned += Register;
        }

        public void ReturnAllToPool()
        {
            flushSnapshot.Clear();
            foreach (var pair in live)
                flushSnapshot.Add(pair);

            foreach (var (instance, entry) in flushSnapshot)
            {
                // A return event earlier in this flush may already have deregistered it.
                if (!live.TryGetValue(instance, out var current) || current != entry) continue;
                if (!instance)
                {
                    Deregister(instance, entry);
                    continue;
                }
                entry.ReturnToPool();
            }
            flushSnapshot.Clear();
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

        // Pooled instances can be destroyed out from under their registration (scene transitions,
        // test teardown) without raising a return event; skip and drop those corpses.
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
