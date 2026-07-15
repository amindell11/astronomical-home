using System.Collections.Generic;
using System.Linq;
using Asteroids.Fields;
using Ships;
using UnityEngine;
using World;

namespace Game.Sectors
{
    /// <summary>Edit-time bake: scoped crawl of a sector's child hierarchy + manifest reconcile; the runtime never crawls. Pure C# (no UnityEditor) so it is unit-testable in EditMode.</summary>
    public static class SectorManifestSync
    {
        public readonly struct ReconcileResult
        {
            public readonly AdoptEntry[] Adopted;
            public readonly SectorSpawner[] Spawners;
            public readonly SectorModule[] Modules;
            public readonly int AppendedAdopt;
            public readonly int AppendedSpawner;
            public readonly int AppendedModule;
            public readonly int OrphanedAdopt;
            public readonly int OrphanedSpawner;
            public readonly int OrphanedModule;

            public ReconcileResult(AdoptEntry[] adopted, SectorSpawner[] spawners, SectorModule[] modules,
                int appendedAdopt, int appendedSpawner, int appendedModule,
                int orphanedAdopt, int orphanedSpawner, int orphanedModule)
            {
                Adopted = adopted;
                Spawners = spawners;
                Modules = modules;
                AppendedAdopt = appendedAdopt;
                AppendedSpawner = appendedSpawner;
                AppendedModule = appendedModule;
                OrphanedAdopt = orphanedAdopt;
                OrphanedSpawner = orphanedSpawner;
                OrphanedModule = orphanedModule;
            }
        }

        public readonly struct DriftReport
        {
            public readonly int UnsyncedChildren;
            public readonly int OrphanedEntries;
            public bool HasDrift => UnsyncedChildren > 0 || OrphanedEntries > 0;

            public DriftReport(int unsyncedChildren, int orphanedEntries)
            {
                UnsyncedChildren = unsyncedChildren;
                OrphanedEntries = orphanedEntries;
            }
        }

        /// <summary>True if the component is a recognised content node that owns its subtree.</summary>
        public static bool IsRecognized(Transform t) =>
            t.GetComponent<SectorSpawner>() ||
            t.GetComponent<WorldRoot>() ||
            t.GetComponent<UpdatingAsteroidField>() ||
            t.GetComponent<Ship>();

        private static bool IsSpawner(Component c) => c is SectorSpawner;

        /// <summary>Returns the recognised content component on a node, preferring a spawner.</summary>
        private static Component Recognized(Transform t)
        {
            if (t.TryGetComponent<SectorSpawner>(out var sp)) return sp;
            if (t.TryGetComponent<WorldRoot>(out var w)) return w;
            if (t.TryGetComponent<UpdatingAsteroidField>(out var f)) return f;
            if (t.TryGetComponent<Ship>(out var s)) return s;
            return null;
        }

        /// <summary>Collect recognised content components in hierarchy order, descending only through plain containers and stopping at each recognised node (it owns its subtree).</summary>
        public static void Collect(Transform root, List<Component> result)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (IsRecognized(child)) result.Add(Recognized(child)); // owns subtree — do NOT descend
                else Collect(child, result);                            // plain container — descend
            }
        }

        /// <summary>Reconcile the manifest against the live hierarchy: preserve existing entries (annotations + order), drop orphans, append new recognised children at the end.</summary>
        public static ReconcileResult Reconcile(
            Transform root, IReadOnlyList<AdoptEntry> existingAdopted, IReadOnlyList<SectorSpawner> existingSpawners,
            IReadOnlyList<SectorModule> existingModules = null)
        {
            var collected = new List<Component>();
            Collect(root, collected);

            var collectedSpawners = new HashSet<Component>();
            var collectedAdopt = new HashSet<Component>();
            foreach (var c in collected)
            {
                if (IsSpawner(c)) collectedSpawners.Add(c);
                else collectedAdopt.Add(c);
            }

            // ── Adopt entries ──
            var keptAdopt = new List<AdoptEntry>();
            var referencedAdopt = new HashSet<Component>();
            var orphanedAdopt = 0;
            if (existingAdopted != null)
            {
                foreach (var e in existingAdopted)
                {
                    if (e.target && collectedAdopt.Contains(e.target))
                    {
                        keptAdopt.Add(e);
                        referencedAdopt.Add(e.target);
                    }
                    else orphanedAdopt++;
                }
            }

            var appendedAdopt = 0;
            foreach (var c in collected)
            {
                if (IsSpawner(c) || referencedAdopt.Contains(c)) continue;
                keptAdopt.Add(new AdoptEntry
                {
                    target = c,
                    team = c is Ship ship ? ship.teamNumber : 0,
                    startActive = true,
                });
                referencedAdopt.Add(c);
                appendedAdopt++;
            }

            // ── Spawner entries ──
            var keptSpawners = new List<SectorSpawner>();
            var referencedSpawners = new HashSet<Component>();
            var orphanedSpawner = 0;
            if (existingSpawners != null)
            {
                foreach (var s in existingSpawners)
                {
                    if (s && collectedSpawners.Contains(s))
                    {
                        keptSpawners.Add(s);
                        referencedSpawners.Add(s);
                    }
                    else orphanedSpawner++;
                }
            }

            var appendedSpawner = 0;
            foreach (var c in collected)
            {
                if (!IsSpawner(c) || referencedSpawners.Contains(c)) continue;
                var s = (SectorSpawner)c;
                keptSpawners.Add(s);
                referencedSpawners.Add(s);
                appendedSpawner++;
            }

            // ── Modules (root components + child GameObjects) ──
            var liveModules = CollectModules(root);
            var keptModules = new List<SectorModule>();
            var referencedModules = new HashSet<SectorModule>();
            var orphanedModule = 0;
            if (existingModules != null)
            {
                foreach (var m in existingModules)
                {
                    if (m && liveModules.Contains(m))
                    {
                        keptModules.Add(m);
                        referencedModules.Add(m);
                    }
                    else orphanedModule++;
                }
            }

            var appendedModule = 0;
            foreach (var m in liveModules)
            {
                if (referencedModules.Contains(m)) continue;
                keptModules.Add(m);
                referencedModules.Add(m);
                appendedModule++;
            }

            return new ReconcileResult(
                keptAdopt.ToArray(), keptSpawners.ToArray(), keptModules.ToArray(),
                appendedAdopt, appendedSpawner, appendedModule,
                orphanedAdopt, orphanedSpawner, orphanedModule);
        }

        /// <summary>Root modules first, then a scoped child crawl: modules ON a recognised content node (e.g. ActivateOnToken on an adopted ship) are collected, its subtree is not.</summary>
        private static List<SectorModule> CollectModules(Transform root)
        {
            var list = new List<SectorModule>();
            root.GetComponents(list);
            CollectChildModules(root, list);
            return list;
        }

        private static void CollectChildModules(Transform parent, List<SectorModule> acc)
        {
            var scratch = new List<SectorModule>();
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                child.GetComponents(scratch);
                acc.AddRange(scratch);
                if (IsRecognized(child)) continue; // content node owns its subtree — do NOT descend
                CollectChildModules(child, acc);
            }
        }

        /// <summary>Read-only drift check: recognised children not yet in the manifest, and manifest entries pointing at deleted/unrecognised targets.</summary>
        public static DriftReport ComputeDrift(
            Transform root, IReadOnlyList<AdoptEntry> adopted, IReadOnlyList<SectorSpawner> spawners,
            IReadOnlyList<SectorModule> modules = null)
        {
            var collected = new List<Component>();
            Collect(root, collected);

            var referenced = new HashSet<Component>();
            var orphaned = 0;
            if (adopted != null)
                foreach (var e in adopted)
                {
                    if (e.target && collected.Contains(e.target)) referenced.Add(e.target);
                    else orphaned++;
                }
            if (spawners != null)
                foreach (var s in spawners)
                {
                    if (s && collected.Contains(s)) referenced.Add(s);
                    else orphaned++;
                }

            var unsynced = collected.Count(c => !referenced.Contains(c));

            // Modules drift (root components) folded into the same badge counts.
            var liveModules = CollectModules(root);
            var referencedModules = new HashSet<SectorModule>();
            if (modules != null)
                foreach (var m in modules)
                {
                    if (m && liveModules.Contains(m)) referencedModules.Add(m);
                    else orphaned++;
                }
            foreach (var m in liveModules)
                if (!referencedModules.Contains(m)) unsynced++;

            return new DriftReport(unsynced, orphaned);
        }
    }
}
