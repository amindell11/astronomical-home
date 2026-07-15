using System.Collections.Generic;
using System.Linq;
using Game.Sectors;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Graph model + validator over the baked manifest: declared outputs, wiring, and cycle findings — plus the all-sector-prefabs sweep that keeps every shipped sector error-free by construction.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorSignalGraphEditModeTests
    {
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

        private Sector NewSector() => NewGO("Sector").AddComponent<Sector>();

        private static List<string> Errors(SectorSignalGraph.Model model) =>
            model.Findings.Where(f => f.Severity == SectorSignalGraph.Severity.Error)
                .Select(f => f.Message).ToList();

        private static List<string> Infos(SectorSignalGraph.Model model) =>
            model.Findings.Where(f => f.Severity == SectorSignalGraph.Severity.Info)
                .Select(f => f.Message).ToList();

        private class StubSpawner : SectorSpawner
        {
            protected override System.Collections.IEnumerator Produce(SectorBuildContext ctx) { yield break; }
        }

        [Test]
        public void CleanChain_HasNoErrors_AndPublishedButUnconsumedIsInfoOnly()
        {
            var sector = NewSector();
            var volumeGO = NewGO("Volume", sector.transform);
            volumeGO.AddComponent<SphereCollider>().isTrigger = true;
            var volume = volumeGO.AddComponent<TriggerVolume>();

            var rule = NewGO("Rule", sector.transform).AddComponent<ActivationRule>();
            rule.Configure(new[] { ActivationTerm.Signal(volume, TriggerVolume.OutputInside) });

            var spawner = NewGO("Spawner", sector.transform).AddComponent<StubSpawner>();
            spawner.ConfigureGated(new SignalRef(rule, ActivationRule.OutputFired));

            var seamRule = NewGO("Seam", sector.transform).AddComponent<ActivationRule>();
            seamRule.Configure(new[] { ActivationTerm.Signal(volume, TriggerVolume.OutputInside) });

            sector.SetManifest(null, new SectorSpawner[] { spawner },
                new SectorModule[] { volume, rule, seamRule });

            var model = SectorSignalGraph.Build(sector);
            Assert.IsEmpty(Errors(model), "A fully wired chain must validate clean.");
            Assert.AreEqual(1, Infos(model).Count, "A deliberate unconsumed output is INFO, never an error.");
            StringAssert.Contains("unconsumed", Infos(model)[0]);
        }

        [Test]
        public void UnassignedConsumerRef_IsError()
        {
            var sector = NewSector();
            var rule = NewGO("Rule", sector.transform).AddComponent<ActivationRule>();
            rule.Configure(new[] { ActivationTerm.Signal(default) });
            sector.SetManifest(null, System.Array.Empty<SectorSpawner>(), new SectorModule[] { rule });

            var errors = Errors(SectorSignalGraph.Build(sector));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("unassigned term signal", errors[0]);
        }

        [Test]
        public void GatedSpawnerWithUnassignedSignal_IsError()
        {
            var sector = NewSector();
            var spawner = NewGO("Spawner", sector.transform).AddComponent<StubSpawner>();
            spawner.ConfigureGated(default);
            sector.SetManifest(null, new SectorSpawner[] { spawner }, System.Array.Empty<SectorModule>());

            var errors = Errors(SectorSignalGraph.Build(sector));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("unassigned activation signal", errors[0]);
        }

        [Test]
        public void CrossSectorRef_IsError()
        {
            var sector = NewSector();
            var foreign = NewSector();
            var foreignRule = NewGO("ForeignRule", foreign.transform).AddComponent<ActivationRule>();

            var rule = NewGO("Rule", sector.transform).AddComponent<ActivationRule>();
            rule.Configure(new[] { ActivationTerm.Signal(foreignRule, ActivationRule.OutputFired) });
            sector.SetManifest(null, System.Array.Empty<SectorSpawner>(), new SectorModule[] { rule });

            var errors = Errors(SectorSignalGraph.Build(sector));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("outside the sector", errors[0]);
        }

        [Test]
        public void UndeclaredOutputRef_IsError()
        {
            var sector = NewSector();
            var donor = NewGO("Donor", sector.transform).AddComponent<ActivationRule>();
            var rule = NewGO("Rule", sector.transform).AddComponent<ActivationRule>();
            rule.Configure(new[] { ActivationTerm.Signal(donor, "bogus") });
            sector.SetManifest(null, System.Array.Empty<SectorSpawner>(), new SectorModule[] { donor, rule });

            var errors = Errors(SectorSignalGraph.Build(sector));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("no sector publisher declares", errors[0]);
        }

        [Test]
        public void RefToPublisherOutsideTheManifest_IsError()
        {
            var sector = NewSector();
            var stray = NewGO("Stray", sector.transform).AddComponent<ActivationRule>();
            var rule = NewGO("Rule", sector.transform).AddComponent<ActivationRule>();
            rule.Configure(new[] { ActivationTerm.Signal(stray, ActivationRule.OutputFired) });
            sector.SetManifest(null, System.Array.Empty<SectorSpawner>(), new SectorModule[] { rule });

            var errors = Errors(SectorSignalGraph.Build(sector));
            Assert.AreEqual(1, errors.Count, "A ref to an in-hierarchy publisher missing from the manifest must be loud — Setup never runs it.");
            StringAssert.Contains("no sector publisher declares", errors[0]);
        }

        [Test]
        public void RuleCycle_IsError()
        {
            var sector = NewSector();
            var a = NewGO("RuleA", sector.transform).AddComponent<ActivationRule>();
            var b = NewGO("RuleB", sector.transform).AddComponent<ActivationRule>();
            a.Configure(new[] { ActivationTerm.Signal(b, ActivationRule.OutputFired) });
            b.Configure(new[] { ActivationTerm.Signal(a, ActivationRule.OutputFired) });
            sector.SetManifest(null, System.Array.Empty<SectorSpawner>(), new SectorModule[] { a, b });

            var errors = Errors(SectorSignalGraph.Build(sector));
            Assert.AreEqual(1, errors.Count, "A two-rule deadlock must be reported exactly once.");
            StringAssert.Contains("cycle", errors[0]);
        }

        [Test]
        public void AllSectorPrefabs_ValidateClean()
        {
            var checkedSectors = 0;
            var errors = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sector = AssetDatabase.LoadAssetAtPath<Sector>(path);
                if (!sector) continue;
                checkedSectors++;
                errors.AddRange(
                    SectorSignalGraph.Build(sector).Findings
                        .Where(f => f.Severity == SectorSignalGraph.Severity.Error)
                        .Select(f => $"{path}: {f.Message}"));
            }

            Assert.GreaterOrEqual(checkedSectors, 5, "Sanity: the sweep must actually find the shipped sector prefabs.");
            Assert.IsEmpty(errors, "Every Sector prefab must validate clean:\n" + string.Join("\n", errors));
        }
    }
}
