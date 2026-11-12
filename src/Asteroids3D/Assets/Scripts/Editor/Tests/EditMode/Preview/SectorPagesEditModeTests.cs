using System;
using System.Collections.Generic;
using System.Linq;
using Game.Preview.Sectors;
using Game.Preview.Fixtures;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Sessions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Preview
{
    [TestFixture]
    [Category("Sectors")]
    public class SectorPagesEditModeTests
    {
        private readonly List<Object> trash = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in trash)
                if (item) Object.DestroyImmediate(item);
            trash.Clear();
        }

        [Test]
        public void List_HoldsTheRunsSectorsThenTheFixtures()
        {
            var run = Track(SectorRun.Create(new SectorEntry(), new SectorEntry(), new SectorEntry()));
            var fixtures = new[]
            {
                new PreviewFixture { layout = Layout("Boss"), values = new SectorEntry() },
                new PreviewFixture { layout = Layout("Arena"), values = new SectorEntry() },
            };
            var pages = new SectorPages(run, fixtures, Array.Empty<Sector>());
            Assert.That(pages.List.Select(page => page.Name).ToArray(),
                Is.EqualTo(new[] { "Sector 1", "Sector 2", "Sector 3", "Boss", "Arena" }));
        }

        [Test]
        public void UsedBy_AllGameRunSectors_AndAValueOnlySomeUse()
        {
            var other = Track(ScriptableObject.CreateInstance<Faction>());
            var partial = Track(ScriptableObject.CreateInstance<Faction>());
            var elite = Track(ScriptableObject.CreateInstance<EliteProfile>());
            var run = Track(SectorRun.Create(
                Row(other, elite), Row(other, elite), Row(partial, elite)));
            var fixtures = new[]
            {
                new PreviewFixture { layout = Layout("Boss"), values = new SectorEntry { faction = partial } },
                new PreviewFixture { layout = Layout("Arena"), values = new SectorEntry { faction = partial } },
                new PreviewFixture { layout = Layout("Crowd"), values = new SectorEntry { elite = elite } },
            };
            var pages = new SectorPages(run, fixtures, Array.Empty<Sector>());

            var shared = pages.Uses(pages.List[0]).Single(value => value.Target == elite);
            Assert.AreEqual("all sectors", shared.UsedBy);

            var some = pages.Uses(pages.List[2]).Single(value => value.Target == partial);
            Assert.AreEqual("Sector 3, Boss, Arena", some.UsedBy);
        }

        [Test]
        public void Checks_BadRow_ReturnsTheMessageCheckRowThrows()
        {
            var root = Track(new GameObject("Sector"));
            root.SetActive(false);
            var sector = root.AddComponent<Sector>();
            var directorObject = new GameObject("WaveDirector");
            directorObject.transform.SetParent(root.transform, false);
            var director = directorObject.AddComponent<WaveDirector>();
            sector.SetManifest(null, new SectorSpawner[] { director }, null);
            var row = new SectorEntry
            {
                prefab = sector,
                phases = new[] { new SectorPhase { cap = 1, groups = Array.Empty<WaveGroup>() } },
            };
            var run = Track(SectorRun.Create(row));
            var thrown = Assert.Throws<InvalidOperationException>(() => sector.CheckRow(row));
            var pages = new SectorPages(run, Array.Empty<PreviewFixture>(), Array.Empty<Sector>());

            Assert.That(pages.Checks(pages.List[0]), Is.EqualTo(new[] { thrown.Message }));
        }

        [Test]
        public void GroupSummary_TwoShipsOfOneTemplate_CountsThemAndListsPilotsInOrder()
        {
            var nightshade = Hull("Nightshade");
            var group = new WaveGroup
            {
                ships = new[]
                {
                    new WaveDirector.RosterEntry { template = nightshade, pilot = Pilot("OrbiterPilot") },
                    new WaveDirector.RosterEntry { template = nightshade, pilot = Pilot("AggressorPilot") },
                },
            };
            Assert.AreEqual("2\u00d7 Nightshade: Orbiter, Aggressor", SectorPages.GroupSummary(group));
        }

        [Test]
        public void GroupSummary_TwoTemplates_JoinsOneShipEachInFirstSeenOrder()
        {
            var group = new WaveGroup
            {
                ships = new[]
                {
                    new WaveDirector.RosterEntry { template = Hull("Nightshade"), pilot = Pilot("OrbiterPilot") },
                    new WaveDirector.RosterEntry { template = Hull("Warden"), pilot = Pilot("AggressorPilot") },
                },
            };
            Assert.AreEqual("Nightshade (Orbiter) + Warden (Aggressor)", SectorPages.GroupSummary(group));
        }

        [Test]
        public void GroupSummary_MissingPilot_ReadsNone()
        {
            var group = new WaveGroup
            {
                ships = new[] { new WaveDirector.RosterEntry { template = Hull("Nightshade") } },
            };
            Assert.AreEqual("Nightshade (none)", SectorPages.GroupSummary(group));
        }

        [Test]
        public void GroupSummary_EmptyGroup_ReadsEmptyGroup()
        {
            Assert.AreEqual("empty group", SectorPages.GroupSummary(default));
            Assert.AreEqual("empty group", SectorPages.GroupSummary(new WaveGroup
            {
                ships = Array.Empty<WaveDirector.RosterEntry>(),
            }));
        }

        private static SectorEntry Row(Faction faction, EliteProfile elite) => new() { faction = faction, elite = elite };

        private Ship Hull(string name)
        {
            var host = Track(new GameObject(name));
            host.SetActive(false);
            return host.AddComponent<Ship>();
        }

        private Commander Pilot(string name)
        {
            var host = Track(new GameObject(name));
            host.SetActive(false);
            return host.AddComponent<NamedPilot>();
        }

        private sealed class NamedPilot : Commander
        {
            public override void Initialize(in ShipControl control) { }
        }

        private Sector Layout(string name)
        {
            var root = Track(new GameObject(name));
            root.SetActive(false);
            return root.AddComponent<Sector>();
        }

        private T Track<T>(T item) where T : Object
        {
            trash.Add(item);
            return item;
        }
    }
}
