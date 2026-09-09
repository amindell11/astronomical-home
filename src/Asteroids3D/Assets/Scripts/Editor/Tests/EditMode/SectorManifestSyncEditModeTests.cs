using System.Collections;
using System.Collections.Generic;
using Asteroids.Fields;
using Game.Sectors;
using NUnit.Framework;
using UnityEngine;
using Game.Sectors.Elements;

namespace Tests.EditMode
{
    /// <summary>Edit-time bake (<see cref="SectorManifestSync"/>) tests — scoped crawl + reconcile rules — using stub spawners/modules so no heavy Ship/World construction is needed.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorManifestSyncEditModeTests
    {
        private class StubSpawner : SectorSpawner
        {
            protected override IEnumerator Produce(SectorBuildContext ctx) { yield break; }
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

            var direct = AddSpawner("Direct", root.transform);
            AddSpawner("NestedInsideRecognized", direct.transform);

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
            var a = AddSpawner("A", root.transform);
            var b = AddSpawner("B", root.transform);
            var c = AddSpawner("C", root.transform);

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
            var a = root.AddComponent<StubModule>();
            var b = root.AddComponent<StubModule>();

            var detached = NewGO("Detached").AddComponent<StubModule>();

            var result = SectorManifestSync.Reconcile(root.transform,
                new AdoptEntry[0], new SectorSpawner[0],
                new SectorModule[] { b, detached });

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

            var container = NewGO("ModuleHolder", root.transform);
            var child = container.AddComponent<StubModule>();

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

            var spawner = AddSpawner("Spawner", root.transform);
            var onNode = spawner.gameObject.AddComponent<StubModule>();

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
        public void Reconcile_ReportsObstacleFieldOnRecognizedNode()
        {
            var root = NewGO("Root");
            var spawner = AddSpawner("Field", root.transform);
            var field = spawner.gameObject.AddComponent<UpdatingAsteroidField>();

            var result = SectorManifestSync.Reconcile(root.transform,
                new AdoptEntry[0], new SectorSpawner[0], new SectorModule[0]);

            Assert.AreSame(field, result.ObstacleField,
                "The asteroid field carried by a recognised node must reach the manifest.");
        }

        [Test]
        public void ComputeDrift_CountsObstacleFieldSlotMismatch()
        {
            var root = NewGO("Root");
            var spawner = AddSpawner("Field", root.transform);
            var field = spawner.gameObject.AddComponent<UpdatingAsteroidField>();
            var stale = NewGO("Stale").AddComponent<UpdatingAsteroidField>();
            var manifest = new SectorSpawner[] { spawner };

            Assert.AreEqual(1, SectorManifestSync.ComputeDrift(root.transform, new AdoptEntry[0], manifest).UnsyncedChildren,
                "An authored field with an empty slot is unsynced.");
            var driftStale = SectorManifestSync.ComputeDrift(root.transform, new AdoptEntry[0], manifest, null, stale);
            Assert.AreEqual(1, driftStale.UnsyncedChildren);
            Assert.AreEqual(1, driftStale.OrphanedEntries, "A slot pointing at a field not in the hierarchy is orphaned.");
            Assert.IsFalse(SectorManifestSync.ComputeDrift(root.transform, new AdoptEntry[0], manifest, null, field).HasDrift,
                "A slot bound to the authored field is in sync.");
        }

        [Test]
        public void ComputeDrift_CountsModuleDrift()
        {
            var root = NewGO("Root");
            var a = root.AddComponent<StubModule>();
            root.AddComponent<StubModule>();
            var detached = NewGO("Detached").AddComponent<StubModule>();

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
            var a = AddSpawner("A", root.transform);
            AddSpawner("B", root.transform);

            var detached = NewGO("Detached").AddComponent<StubSpawner>();

            var drift = SectorManifestSync.ComputeDrift(root.transform,
                new AdoptEntry[0], new SectorSpawner[] { a, detached });

            Assert.AreEqual(1, drift.UnsyncedChildren, "B is recognised but not in the manifest.");
            Assert.AreEqual(1, drift.OrphanedEntries, "Detached entry points at a node not in the hierarchy.");
            Assert.IsTrue(drift.HasDrift);
        }
    }
}
