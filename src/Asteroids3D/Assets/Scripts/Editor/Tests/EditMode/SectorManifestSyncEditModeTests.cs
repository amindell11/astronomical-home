using System.Collections;
using System.Collections.Generic;
using Game.Sectors;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the edit-time bake (<see cref="SectorManifestSync"/>): the scoped crawl
    /// (descend plain containers, stop at recognised nodes) and reconcile rules (append new, drop
    /// orphans, preserve order). Uses lightweight <see cref="SectorSpawner"/> stubs as the
    /// recognised nodes so no heavy Ship/World construction is needed.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorManifestSyncEditModeTests
    {
        private class StubSpawner : SectorSpawner
        {
            public override IEnumerator Build(SectorBuildContext ctx) { yield break; }
        }

        private class StubModule : SectorModule { }

        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject NewGO(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent);
            else _created.Add(go);
            return go;
        }

        private StubSpawner AddSpawner(string name, Transform parent)
        {
            var go = NewGO(name, parent);
            return go.AddComponent<StubSpawner>();
        }

        [Test]
        public void Collect_DescendsContainers_StopsAtRecognizedNodes()
        {
            var root = NewGO("Root");

            // Recognised node directly under root.
            var direct = AddSpawner("Direct", root.transform);

            // Recognised node NESTED inside another recognised node — must NOT be collected
            // (the outer node owns its subtree; scoped crawl stops at it).
            AddSpawner("NestedInsideRecognized", direct.transform);

            // Plain container with a recognised node inside — container is transparent, so the
            // inner node IS collected.
            var container = NewGO("PlainContainer", root.transform);
            var inContainer = AddSpawner("InContainer", container.transform);

            var collected = new List<Component>();
            SectorManifestSync.Collect(root.transform, collected);

            CollectionAssert.Contains(collected, direct, "Direct recognised child must be collected.");
            CollectionAssert.Contains(collected, inContainer, "Recognised node inside a plain container must be collected.");
            Assert.AreEqual(2, collected.Count,
                "Scoped crawl must not descend into a recognised node's subtree.");
        }

        [Test]
        public void Reconcile_AppendsNewSpawners()
        {
            var root = NewGO("Root");
            var a = AddSpawner("A", root.transform);
            var b = AddSpawner("B", root.transform);

            var result = SectorManifestSync.Reconcile(root.transform,
                new AdoptEntry[0], new SectorSpawner[0]);

            Assert.AreEqual(2, result.AppendedSpawner);
            Assert.AreEqual(2, result.Spawners.Length);
            CollectionAssert.Contains(result.Spawners, a);
            CollectionAssert.Contains(result.Spawners, b);
        }

        [Test]
        public void Reconcile_DropsOrphanedEntries()
        {
            var root = NewGO("Root");
            var a = AddSpawner("A", root.transform);

            // Existing manifest references a spawner that is NOT in the hierarchy → orphan.
            var detached = NewGO("Detached").AddComponent<StubSpawner>();

            var result = SectorManifestSync.Reconcile(root.transform,
                new AdoptEntry[0], new SectorSpawner[] { detached });

            Assert.AreEqual(1, result.OrphanedSpawner, "Detached spawner entry must be reported as orphan.");
            Assert.AreEqual(1, result.Spawners.Length);
            CollectionAssert.Contains(result.Spawners, a);
            CollectionAssert.DoesNotContain(result.Spawners, detached);
        }

        [Test]
        public void Reconcile_PreservesExistingOrder_AppendsNewAtEnd()
        {
            var root = NewGO("Root");
            // Hierarchy order: A, B, C.
            var a = AddSpawner("A", root.transform);
            var b = AddSpawner("B", root.transform);
            var c = AddSpawner("C", root.transform);

            // Existing manifest order is intentionally different: C, A. (B is new.)
            var result = SectorManifestSync.Reconcile(root.transform,
                new AdoptEntry[0], new SectorSpawner[] { c, a });

            Assert.AreEqual(3, result.Spawners.Length);
            Assert.AreEqual(c, result.Spawners[0], "Existing order must be preserved (C first).");
            Assert.AreEqual(a, result.Spawners[1], "Existing order must be preserved (A second).");
            Assert.AreEqual(b, result.Spawners[2], "New spawner must be appended at the end.");
            Assert.AreEqual(1, result.AppendedSpawner);
            Assert.AreEqual(0, result.OrphanedSpawner);
        }

        [Test]
        public void Reconcile_CollectsRootModules_AppendsNew_PreservesOrder_DropsOrphans()
        {
            var root = NewGO("Root");
            // Modules live as components on the sector ROOT, not as crawled children.
            var a = root.AddComponent<StubModule>();
            var b = root.AddComponent<StubModule>();

            // Existing manifest references an orphan module on a detached object.
            var detached = NewGO("Detached").AddComponent<StubModule>();

            var result = SectorManifestSync.Reconcile(root.transform,
                new AdoptEntry[0], new SectorSpawner[0],
                new SectorModule[] { b, detached });

            // b kept (still on root, preserves position 0), detached dropped, a appended.
            Assert.AreEqual(1, result.AppendedModule, "Module 'a' is new and must be appended.");
            Assert.AreEqual(1, result.OrphanedModule, "Detached module entry must be reported as orphan.");
            Assert.AreEqual(2, result.Modules.Length);
            Assert.AreEqual(b, result.Modules[0], "Existing module order preserved (b first).");
            Assert.AreEqual(a, result.Modules[1], "New module appended at the end.");
        }

        [Test]
        public void Reconcile_CollectsChildModules_ScopedStopAtContentNodes()
        {
            var root = NewGO("Root");

            // Module authored as a child GameObject (under a plain container) — must be collected.
            var container = NewGO("ModuleHolder", root.transform);
            var child = container.AddComponent<StubModule>();

            // Module nested INSIDE a recognised content node — must NOT be collected (scoped stop).
            var spawner = AddSpawner("Spawner", root.transform);
            var hidden = NewGO("Hidden", spawner.transform).AddComponent<StubModule>();

            var result = SectorManifestSync.Reconcile(root.transform,
                new AdoptEntry[0], new SectorSpawner[0], new SectorModule[0]);

            CollectionAssert.Contains(result.Modules, child, "Module on a child GameObject must be collected.");
            CollectionAssert.DoesNotContain(result.Modules, hidden,
                "Module under a recognised content node must NOT be collected (scoped stop).");
        }

        [Test]
        public void Reconcile_CollectsModuleOnRecognizedNode_WithoutDescending()
        {
            var root = NewGO("Root");

            // Module ON a recognised content node (e.g. ActivateOnToken on an adopted ship) — collected.
            var spawner = AddSpawner("Spawner", root.transform);
            var onNode = spawner.gameObject.AddComponent<StubModule>();

            // Module INSIDE that node's subtree — still owned by the node, NOT collected.
            var hidden = NewGO("Hidden", spawner.transform).AddComponent<StubModule>();

            var result = SectorManifestSync.Reconcile(root.transform,
                new AdoptEntry[0], new SectorSpawner[0], new SectorModule[0]);

            CollectionAssert.Contains(result.Modules, onNode,
                "A module carried by a recognised content node must be collected.");
            CollectionAssert.DoesNotContain(result.Modules, hidden,
                "The node's subtree stays owned — no descent.");

            var drift = SectorManifestSync.ComputeDrift(root.transform,
                new AdoptEntry[0], new SectorSpawner[] { spawner }, new SectorModule[] { onNode });
            Assert.IsFalse(drift.HasDrift, "A synced on-node module must not read as drift.");
        }

        [Test]
        public void ComputeDrift_CountsModuleDrift()
        {
            var root = NewGO("Root");
            var a = root.AddComponent<StubModule>(); // in manifest
            root.AddComponent<StubModule>();         // unsynced (not in manifest)
            var detached = NewGO("Detached").AddComponent<StubModule>(); // orphan entry

            var drift = SectorManifestSync.ComputeDrift(root.transform,
                new AdoptEntry[0], new SectorSpawner[0],
                new SectorModule[] { a, detached });

            Assert.AreEqual(1, drift.UnsyncedChildren, "Second root module is recognised but unsynced.");
            Assert.AreEqual(1, drift.OrphanedEntries, "Detached module entry is orphaned.");
            Assert.IsTrue(drift.HasDrift);
        }

        [Test]
        public void ComputeDrift_CountsUnsyncedAndOrphaned()
        {
            var root = NewGO("Root");
            var a = AddSpawner("A", root.transform);   // in hierarchy
            AddSpawner("B", root.transform);           // in hierarchy, unsynced

            var detached = NewGO("Detached").AddComponent<StubSpawner>(); // orphan entry

            var drift = SectorManifestSync.ComputeDrift(root.transform,
                new AdoptEntry[0], new SectorSpawner[] { a, detached });

            Assert.AreEqual(1, drift.UnsyncedChildren, "B is recognised but not in the manifest.");
            Assert.AreEqual(1, drift.OrphanedEntries, "Detached entry points at a node not in the hierarchy.");
            Assert.IsTrue(drift.HasDrift);
        }
    }
}
