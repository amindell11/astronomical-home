using System;
using System.Collections.Generic;
using System.Linq;
using Asteroids.Fields;
using Ships;
using UnityEngine;

namespace Substrate.Sectors.Elements
{
    /// <summary>Edit-time bake: scoped crawl of a sector's child hierarchy + manifest reconcile; the runtime never crawls. Pure C# (no UnityEditor) so it is unit-testable in EditMode.</summary>
    public static class SectorManifestSync
    {
        public readonly struct ReconcileResult
        {
            public readonly AdoptedShip[] Adopted;
            public readonly SectorSpawner[] Spawners;
            public readonly SectorModule[] Modules;
            /// <summary>The single authored asteroid field, or null; a second one is an authoring error and throws at bake.</summary>
            public readonly UpdatingAsteroidField ObstacleField;
            public readonly int AppendedAdopt;
            public readonly int AppendedSpawner;
            public readonly int AppendedModule;
            public readonly int OrphanedAdopt;
            public readonly int OrphanedSpawner;
            public readonly int OrphanedModule;

            public ReconcileResult(AdoptedShip[] adopted, SectorSpawner[] spawners, SectorModule[] modules,
                UpdatingAsteroidField obstacleField,
                int appendedAdopt, int appendedSpawner, int appendedModule,
                int orphanedAdopt, int orphanedSpawner, int orphanedModule)
            {
                Adopted = adopted;
                Spawners = spawners;
                Modules = modules;
                ObstacleField = obstacleField;
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
            t.GetComponent<UpdatingAsteroidField>() ||
            t.GetComponent<Ship>();

        private static bool IsSpawner(Component c) => c is SectorSpawner;

        /// <summary>Returns the recognised content component on a node, preferring a spawner.</summary>
        private static Component Recognized(Transform t)
        {
            if (t.TryGetComponent<SectorSpawner>(out var sp)) return sp;
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
                if (IsRecognized(child)) result.Add(Recognized(child));
                else Collect(child, result);
            }
        }

        /// <summary>Reconcile the manifest against the live hierarchy: preserve existing entries (annotations + order), drop orphans, append new recognised children at the end.</summary>
        public static ReconcileResult Reconcile(
            Transform root, IReadOnlyList<AdoptedShip> existingAdopted, IReadOnlyList<SectorSpawner> existingSpawners,
            IReadOnlyList<SectorModule> existingModules = null)
        {
            var collected = new List<Component>();
            Collect(root, collected);

            var collectedSpawners = new HashSet<Component>();
            var collectedShips = new HashSet<Ship>();
            foreach (var c in collected)
            {
                if (IsSpawner(c)) collectedSpawners.Add(c);
                else if (c is Ship ship) collectedShips.Add(ship);
            }

            var keptAdopt = new List<AdoptedShip>();
            var referencedAdopt = new HashSet<Ship>();
            var orphanedAdopt = 0;
            if (existingAdopted != null)
            {
                foreach (var e in existingAdopted)
                {
                    if (e.target && collectedShips.Contains(e.target))
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
                if (!(c is Ship ship) || referencedAdopt.Contains(ship)) continue;
                keptAdopt.Add(new AdoptedShip
                {
                    target = ship,
                    team = ship.teamNumber,
                    startActive = true,
                });
                referencedAdopt.Add(ship);
                appendedAdopt++;
            }

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
                SingleObstacleField(collected),
                appendedAdopt, appendedSpawner, appendedModule,
                orphanedAdopt, orphanedSpawner, orphanedModule);
        }

        /// <summary>The field ON a recognised node (the field prefab's spawner wins recognition, so the crawl never yields the field itself).</summary>
        private static UpdatingAsteroidField SingleObstacleField(List<Component> collected)
        {
            UpdatingAsteroidField found = null;
            foreach (var c in collected)
            {
                if (!c.TryGetComponent<UpdatingAsteroidField>(out var field)) continue;
                if (found)
                    throw new InvalidOperationException(
                        $"Sector authors two asteroid fields ('{found.name}', '{field.name}'); a world has one obstacle field.");
                found = field;
            }
            return found;
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
                if (!IsRecognized(child)) CollectChildModules(child, acc);
            }
        }

        /// <summary>Read-only drift check: recognised children not yet in the manifest, manifest entries pointing at deleted/unrecognised targets, and an obstacle-field slot that disagrees with the authored field.</summary>
        public static DriftReport ComputeDrift(
            Transform root, IReadOnlyList<AdoptedShip> adopted, IReadOnlyList<SectorSpawner> spawners,
            IReadOnlyList<SectorModule> modules = null, UpdatingAsteroidField obstacleField = null)
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

            // A bare field node is tracked by the obstacle-field slot below, not by a manifest list.
            var unsynced = collected.Count(c => !(c is UpdatingAsteroidField) && !referenced.Contains(c));

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

            var liveField = SingleObstacleField(collected);
            if (liveField != obstacleField)
            {
                if (liveField) unsynced++;
                if (obstacleField) orphaned++;
            }

            return new DriftReport(unsynced, orphaned);
        }
    }
}
