using System;
using System.Collections.Generic;
using Asteroids.Fields;
using Game;
using Game.Preview;
using Game.Preview.Fixtures;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Services.Locales;
using Substrate.Sessions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Preview
{
    [TestFixture]
    [Category("Bootstrap")]
    public class PreviewFixtureEditModeTests
    {
        private readonly List<Object> trash = new();
        private Sector layout;
        private Sector rowLayout;
        private Faction factionA;
        private Faction factionB;
        private EliteProfile eliteA;
        private EliteProfile eliteB;
        private AsteroidFieldSettings fieldA;
        private AsteroidFieldSettings fieldB;
        private LocaleSky localeA;
        private LocaleSky localeB;
        private SectorEntry row;
        private SectorRun run;

        [SetUp]
        public void SetUp()
        {
            layout = Layout("VeinLayout");
            rowLayout = Layout("RowLayout");
            factionA = Track(ScriptableObject.CreateInstance<Faction>());
            factionB = Track(ScriptableObject.CreateInstance<Faction>());
            eliteA = Track(ScriptableObject.CreateInstance<EliteProfile>());
            eliteB = Track(ScriptableObject.CreateInstance<EliteProfile>());
            fieldA = Track(ScriptableObject.CreateInstance<AsteroidFieldSettings>());
            fieldB = Track(ScriptableObject.CreateInstance<AsteroidFieldSettings>());
            localeA = Locale("LocaleA");
            localeB = Locale("LocaleB");
            row = new SectorEntry
            {
                prefab = rowLayout,
                locale = localeA,
                random = true,
                faction = factionA,
                elite = eliteA,
                phases = new[] { new SectorPhase { seconds = 4 } },
                patrols = 2,
                field = fieldA
            };
            run = Track(SectorRun.Create(row));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in trash)
                if (item) Object.DestroyImmediate(item);
            trash.Clear();
        }

        [Test]
        public void UntickedField_FollowsTheLinkedRow()
        {
            var fixture = Linked(new SectorEntry
            {
                faction = factionB,
                elite = eliteB,
                phases = new[] { new SectorPhase { seconds = 1 } },
                patrols = 9,
                field = fieldB,
                locale = localeB,
                random = false
            });

            var resolved = fixture.Resolve(run);
            AssertSameRow(resolved, factionA, eliteA, row.phases, 2, fieldA, localeA, true);

            row.faction = factionB;
            row.elite = eliteB;
            row.phases = new[] { new SectorPhase { seconds = 8 } };
            row.patrols = 7;
            row.field = fieldB;
            row.locale = localeB;
            row.random = false;

            resolved = fixture.Resolve(run);
            AssertSameRow(resolved, factionB, eliteB, row.phases, 7, fieldB, localeB, false);
            Assert.AreSame(rowLayout, row.prefab);
        }

        [Test]
        public void TickedField_KeepsItsOwnValue()
        {
            var ownPhases = new[] { new SectorPhase { seconds = 5 } };
            var fixture = Linked(new SectorEntry
            {
                faction = factionB,
                elite = eliteB,
                phases = ownPhases,
                patrols = 9,
                field = fieldB,
                locale = localeB,
                random = false
            }, PreviewFixtureOwn.Patrols | PreviewFixtureOwn.Locale | PreviewFixtureOwn.Waves);

            var resolved = fixture.Resolve(run);
            Assert.AreEqual(9, resolved.patrols);
            Assert.AreSame(localeB, resolved.locale);
            Assert.IsFalse(resolved.random);
            Assert.AreSame(ownPhases, resolved.phases);
            Assert.AreSame(factionA, resolved.faction);
            Assert.AreSame(eliteA, resolved.elite);
            Assert.AreSame(fieldA, resolved.field);

            row.patrols = 1;
            row.locale = localeA;
            row.random = true;
            row.phases = new[] { new SectorPhase { seconds = 11 } };
            row.faction = factionB;
            row.elite = eliteB;
            row.field = fieldB;

            resolved = fixture.Resolve(run);
            Assert.AreEqual(9, resolved.patrols);
            Assert.AreSame(localeB, resolved.locale);
            Assert.IsFalse(resolved.random);
            Assert.AreSame(ownPhases, resolved.phases);
            Assert.AreSame(factionB, resolved.faction);
            Assert.AreSame(eliteB, resolved.elite);
            Assert.AreSame(fieldB, resolved.field);
            Assert.AreSame(layout, resolved.prefab);
        }

        [Test]
        public void UnlinkedFixture_ReadsNoRow()
        {
            var phases = new[] { new SectorPhase { seconds = 6 } };
            var fixture = new PreviewFixture
            {
                layout = layout,
                values = new SectorEntry
                {
                    prefab = rowLayout,
                    faction = factionB,
                    elite = eliteB,
                    phases = phases,
                    patrols = 4,
                    field = fieldB,
                    locale = localeB,
                    random = false
                }
            };

            row.faction = factionA;
            row.patrols = 1;
            row.locale = localeA;
            row.random = true;

            var resolved = fixture.Resolve(run);
            Assert.AreSame(layout, resolved.prefab);
            Assert.AreSame(factionB, resolved.faction);
            Assert.AreSame(eliteB, resolved.elite);
            Assert.AreSame(phases, resolved.phases);
            Assert.AreEqual(4, resolved.patrols);
            Assert.AreSame(fieldB, resolved.field);
            Assert.AreSame(localeB, resolved.locale);
            Assert.IsFalse(resolved.random);

            row.faction = factionA;
            row.patrols = 8;
            resolved = fixture.Resolve(run);
            Assert.AreSame(factionB, resolved.faction);
            Assert.AreEqual(4, resolved.patrols);
        }

        [Test]
        public void Resolve_DoesNotMutateTheRunRow()
        {
            var fixture = Linked(new SectorEntry
            {
                faction = factionB,
                elite = eliteB,
                phases = new[] { new SectorPhase { seconds = 1 } },
                patrols = 9,
                field = fieldB,
                locale = localeB,
                random = false
            });
            var phases = row.phases;

            var resolved = fixture.Resolve(run);

            Assert.AreNotSame(row, resolved);
            resolved.prefab = layout;
            resolved.locale = localeB;
            resolved.random = false;
            resolved.faction = factionB;
            resolved.elite = eliteB;
            resolved.phases = Array.Empty<SectorPhase>();
            resolved.patrols = 99;
            resolved.field = fieldB;

            Assert.AreSame(rowLayout, row.prefab);
            Assert.AreSame(localeA, row.locale);
            Assert.IsTrue(row.random);
            Assert.AreSame(factionA, row.faction);
            Assert.AreSame(eliteA, row.elite);
            Assert.AreSame(phases, row.phases);
            Assert.AreEqual(2, row.patrols);
            Assert.AreSame(fieldA, row.field);
        }

        [Test]
        public void LinkOutsideTheRun_NamesTheLayoutAndTheIndex()
        {
            var fixture = new PreviewFixture { layout = layout, link = 2, values = new SectorEntry() };
            var ex = Assert.Throws<InvalidOperationException>(() => fixture.Resolve(run));
            Assert.That(ex.Message, Does.Contain("VeinLayout").And.Contain("Sector 2"));
            Assert.AreSame(rowLayout, row.prefab);
            Assert.AreEqual(2, row.patrols);
        }

        [Test]
        public void OneRowFixtureRun_CarriesTheGameRunsLocalePool()
        {
            var game = AssetDatabase.LoadAssetAtPath<GameHost>(PreviewHostBootstrap.GameHostPath);
            var root = Track(new GameObject("One row fixture run"));
            root.SetActive(false);
            var host = root.AddComponent<PreviewHost>();
            host.gameHostPrefab = game;
            var fixture = new PreviewFixture
            {
                layout = layout,
                link = PreviewFixture.Unlinked,
                values = new SectorEntry()
            };

            var flown = Track(host.OneRowFixtureRun(fixture));

            Assert.That(flown, Is.Not.SameAs(game.sessionProfile.run));
            Assert.That(flown.LocalePool, Is.EqualTo(game.sessionProfile.run.LocalePool));
            Assert.That(flown.LocalePool, Is.Not.SameAs(game.sessionProfile.run.LocalePool));
            Assert.That(flown.Sectors.Count, Is.EqualTo(1));
            Assert.That(flown.Sectors[0].prefab, Is.SameAs(layout));
        }

        private PreviewFixture Linked(SectorEntry values, PreviewFixtureOwn own = PreviewFixtureOwn.None) =>
            new() { layout = layout, link = 1, own = own, values = values };

        private void AssertSameRow(SectorEntry resolved, Faction faction, EliteProfile elite, SectorPhase[] phases,
            int patrols, AsteroidFieldSettings field, LocaleSky locale, bool random)
        {
            Assert.AreSame(layout, resolved.prefab);
            Assert.AreSame(faction, resolved.faction);
            Assert.AreSame(elite, resolved.elite);
            Assert.AreSame(phases, resolved.phases);
            Assert.AreEqual(patrols, resolved.patrols);
            Assert.AreSame(field, resolved.field);
            Assert.AreSame(locale, resolved.locale);
            Assert.AreEqual(random, resolved.random);
        }

        private Sector Layout(string name)
        {
            var root = Track(new GameObject(name));
            root.SetActive(false);
            return root.AddComponent<Sector>();
        }

        private LocaleSky Locale(string name)
        {
            var root = Track(new GameObject(name));
            root.SetActive(false);
            return root.AddComponent<LocaleSky>();
        }

        private T Track<T>(T item) where T : Object
        {
            trash.Add(item);
            return item;
        }
    }
}
