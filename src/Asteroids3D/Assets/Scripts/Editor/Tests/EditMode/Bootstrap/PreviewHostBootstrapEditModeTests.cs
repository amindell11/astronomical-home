using System.Linq;
using Game;
using Game.Preview;
using Game.Preview.Fixtures;
using NUnit.Framework;
using Substrate.Services.Locales;
using UI.Projection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Asteroids.Fields;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Sessions;

namespace Tests.EditMode.Bootstrap
{
    [Category("Bootstrap")]
    public class PreviewHostBootstrapEditModeTests
    {
        private PreviewHost host;

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host.gameObject);
        }

        [TestCase(PreviewEntry.RunRecap)]
        [TestCase(PreviewEntry.Combat)]
        public void BuildHost_AssignsEntryTheShippedGameHostAndTheFixtureSectors(PreviewEntry entry)
        {
            host = PreviewHostBootstrap.BuildHost(entry);

            Assert.AreEqual(entry, host.Entry);
            Assert.IsFalse(host.gameObject.activeSelf, "the host waits for play mode to take the snapshot");
            Assert.AreEqual(PreviewHostBootstrap.GameHostPath, AssetDatabase.GetAssetPath(host.gameHostPrefab),
                "Playtest runs the GameHost prefab the boot scene instances");
            Assert.IsTrue(host.sessionProfile.presentation, "the preview shows presentation");
            var run = host.gameHostPrefab.sessionProfile.run;
            Assert.That(host.sessionProfile.run, Is.SameAs(run), "Replay flies the game's own sector run");
            Assert.That(host.crowdedJumpSectors.Select(fixture => fixture.layout.name),
                Is.EqualTo(new[] { "CrowdedJumpDeparture", "CrowdedJumpArrival" }));
            Assert.That(host.crowdedJumpSectors.Select(fixture => fixture.Resolve(run).locale.name),
                Is.EqualTo(new[] { "IllustratedBlue", "CopperTurquoise" }));
            Assert.That(host.crowdedJumpSectors.Select(fixture => fixture.link), Is.All.EqualTo(PreviewFixture.Unlinked),
                "the crowded jump's fixtures are unlinked");
            var hud = new[] { host.hudQuietSector, host.hudExtractionSector };
            Assert.That(hud.Select(fixture => fixture.layout.name),
                Is.EqualTo(new[] { "HudQuietSector", "HudExtractionSector" }));
            Assert.AreEqual("EliteSector1", host.sector1EliteSector.layout.name);
            Assert.AreEqual("EliteSector2", host.sector2EliteSector.layout.name);
            Assert.AreEqual("BossSector", host.bossSector.layout.name);
            var fixtures = AssetDatabase.LoadAssetAtPath<PreviewFixtures>(PreviewHostBootstrap.FixturesPath).Entries;
            var flown = host.crowdedJumpSectors.Concat(hud)
                .Append(host.bossSector).Append(host.sector1EliteSector).Append(host.sector2EliteSector)
                .Append(host.crowdedFightSector).ToArray();
            Assert.That(flown, Is.EquivalentTo(fixtures), "each host field is that layout's fixture entry");
            Assert.That(fixtures.Where(fixture => fixture.layout.name != "CrowdedJumpArrival")
                    .Select(fixture => fixture.Resolve(run).locale),
                Is.All.SameAs(run.LocalePool[0]), "every fixture but the jump's arrival shows the run pool's first locale");
            Assert.That(host.bossSector.Resolve(run).elite.name, Is.EqualTo("BastionBoss"),
                "the boss fixture ends at sector 3's elite");
        }

        [Test]
        public void CombatRun_WithNoPick_IsTheGameHostsOwnRun()
        {
            host = PreviewHostBootstrap.BuildHost(PreviewEntry.Combat);

            Assert.That(host.combatSector, Is.EqualTo(PreviewHost.WholeRun));
            Assert.IsFalse(host.combatLocale);
            Assert.That(host.CombatRun(), Is.SameAs(host.gameHostPrefab.sessionProfile.run));
        }

        [TestCase(1, "CoralSunset")]
        [TestCase(PreviewHost.WholeRun, "CoralSunset")]
        [TestCase(1, null)]
        public void CombatRun_FliesTheSectorPickAlone_InTheLocalePick_AndLeavesTheRunAsset(int sector, string locale)
        {
            var run = AssetDatabase.LoadAssetAtPath<GameHost>(PreviewHostBootstrap.GameHostPath).sessionProfile.run;
            var shipped = run.Sectors.Select(row => (row.prefab, row.locale, row.random)).ToArray();
            var values = run.Sectors.Select(Values).ToArray();
            var picked = locale == null ? null : AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/{locale}.prefab");
            host = PreviewHostBootstrap.BuildHost(PreviewEntry.Combat, sector, picked);

            var combat = host.CombatRun();

            var flown = sector == PreviewHost.WholeRun ? shipped : new[] { shipped[sector] };
            Assert.That(combat, Is.Not.SameAs(run));
            Assert.That(combat.Sectors.Select(row => row.prefab), Is.EqualTo(flown.Select(row => row.prefab)));
            Assert.That(combat.Sectors.Select(row => row.locale), Is.EqualTo(flown.Select(row => picked ? picked : row.locale)));
            Assert.That(combat.Sectors.Select(row => row.random), Is.EqualTo(flown.Select(row => !picked && row.random)));
            Assert.That(combat.Sectors.Select(Values),
                Is.EqualTo(sector == PreviewHost.WholeRun ? values : new[] { values[sector] }),
                "each flown row carries its faction, elite, phases, patrols and field");
            Assert.That(combat.LocalePool, Is.EqualTo(run.LocalePool));
            Assert.That(run.Sectors.Select(row => (row.prefab, row.locale, row.random)), Is.EqualTo(shipped), "a pick never writes to the run asset");
            Object.DestroyImmediate(combat);
        }

        private static (Faction, EliteProfile, SectorPhase[], int, AsteroidFieldSettings)
            Values(SectorEntry row) => (row.faction, row.elite, row.phases, row.patrols, row.field);

        [Test]
        public void Locales_ListsEveryLocalePrefabInTheLocalesFolder_ByName()
        {
            var locales = PreviewHostBootstrap.Locales();

            var names = locales.Select(locale => locale.name).ToArray();
            Assert.That(names, Is.SupersetOf(new[]
                { "BlueCrescent", "CopperTurquoise", "CoralSunset", "CyanAurora", "IllustratedBlue", "VerdantGiant" }));
            Assert.That(names, Is.EqualTo(names.OrderBy(name => name)), "listed by name");
            Assert.That(locales.Select(locale => AssetDatabase.GetAssetPath(locale)), Is.All.StartWith("Assets/Prefabs/Locales/"));
        }

        [Test]
        public void Activate_SnapshotsTheFeelsTheGameShips_ThenActivatesTheHost()
        {
            host = PreviewHostBootstrap.BuildHost();
            var snapshot = FeelSnapshot.instance;
            try
            {
                PreviewHostBootstrap.Activate(host, snapshot);

                Assert.IsTrue(host.gameObject.activeSelf);
                var game = host.gameHostPrefab;
                var canopy = AssetDatabase.LoadAssetAtPath<CanopyProjectionProfile>("Assets/Settings/UI/CanopyProjection.asset");
                Assert.That(HudTarget.CanopyProfile(game), Is.SameAs(canopy), "the rig's canopy prefab ships it");
                foreach (var feel in TuningTarget.ProfilesOfAll(game).Append(canopy))
                    Assert.IsFalse(snapshot.HasUnsavedEdits(feel), $"{feel.name} is under the snapshot");
            }
            finally
            {
                snapshot.Close();
            }
        }

        [Test]
        public void BootScene_InstancesTheGameHostPrefab_WithNoOverrides()
        {
            const string scenePath = "Assets/Scenes/InitScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var bootHost = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<GameHost>(true)).Single(h => h);
                Assert.AreEqual(PreviewHostBootstrap.GameHostPath,
                    AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(bootHost)));
                Assert.IsFalse(PrefabUtility.HasPrefabInstanceAnyOverrides(bootHost.gameObject, false),
                    "Playtest runs the prefab, so a boot-scene override would split it from the shipped game");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
