using Game;
using Substrate.Sectors;
using Substrate.Services.Locales;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Ships;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;

namespace Tests.EditMode
{
    [Category("Bootstrap")]
    public class SessionContractsEditModeTests
    {
        private const string RunPath = "Assets/Settings/Game/SurvivalTrialRun.asset";
        private const string ArenaPath = "Assets/Prefabs/Sectors/ArenaSector.prefab";
        private const string LocalePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";
        private const string OtherLocalePath = "Assets/Prefabs/Locales/CoralSunset.prefab";

        private GameObject owner;
        private SectorRun run;

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            if (run) Object.DestroyImmediate(run);
        }

        private void Construct(SectorEntry second, bool presentation) =>
            Construct(presentation, System.Array.Empty<LocaleSky>(), NamedRow(), second);

        // A throw at construction precedes the session scene, so edit mode can host it.
        private void Construct(bool presentation, LocaleSky[] pool, params SectorEntry[] rows)
        {
            owner = new GameObject("Session owner");
            run = SectorRun.Create(pool, rows);
            _ = new Session(new SessionProfile { run = run, presentation = presentation }, owner,
                owner.AddComponent<UnitService>(), owner.AddComponent<ObjectiveService>());
        }

        private static SectorEntry NamedRow() => TrialRow().WithLocale(Locale(LocalePath));

        private static SectorEntry RandomRow() => TrialRow().WithLocale(null, drawn: true);

        // A copy, so a test that blanks a value never edits the run asset.
        private static SectorEntry TrialRow() => AssetDatabase.LoadAssetAtPath<SectorRun>(RunPath).Sectors[0].WithLocale(null);

        private static LocaleSky Locale(string path) => AssetDatabase.LoadAssetAtPath<LocaleSky>(path);

        [Test]
        public void ISector_DeclaresItsLifecycleMembers()
        {
            foreach (var name in new[] { "Initialize", "Setup", "Teardown" })
                Assert.IsNotNull(typeof(ISector).GetMethod(name), $"ISector must declare {name}");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Session_RefusesARunRowWithNoSector(bool presentation)
        {
            var row = new SectorEntry { locale = AssetDatabase.LoadAssetAtPath<LocaleSky>(LocalePath) };
            Assert.That(() => Construct(row, presentation),
                Throws.ArgumentException.With.Message.Contains("Row 1 of sector run").And.Message.Contains("no sector"));
        }

        [Test]
        public void Session_RefusesAPresentedRunRowWithNoLocale()
        {
            var row = TrialRow();
            Assert.That(() => Construct(row, presentation: true),
                Throws.ArgumentException.With.Message.Contains("Row 1 of sector run").And.Message.Contains("no locale"));
        }

        [TestCase("faction", "the row's faction")]
        [TestCase("elite", "EliteEncounter")]
        [TestCase("groups", "WaveDirector")]
        [TestCase("phases", "WaveDirector")]
        [TestCase("field", "asteroid field")]
        public void Session_RefusesARowMissingAValueItsLayoutReads(string value, string reader)
        {
            var row = TrialRow();
            switch (value)
            {
                case "faction": row.faction = null; break;
                case "elite": row.elite = null; break;
                case "groups": row.phases = WithoutGroups(row.phases); break;
                case "phases": row.phases = System.Array.Empty<SectorPhase>(); break;
                case "field": row.field = null; break;
            }
            Assert.That(() => Construct(row, presentation: false),
                Throws.ArgumentException.With.Message.Contains("Row 1 of sector run").And.Message.Contains(reader));
        }

        [Test]
        public void Session_RefusesARosterShipTheRowsFactionDoesNotFly()
        {
            var row = TrialRow();
            var bulwark = new WaveGroup
            {
                ships = new[] { new WaveDirector.RosterEntry { template = TestShip("Bulwark") } },
            };
            var phases = new SectorPhase[row.phases.Length];
            for (var i = 0; i < phases.Length; i++)
            {
                phases[i] = row.phases[i];
                phases[i].groups = new[] { bulwark };
            }
            row.phases = phases;
            Assert.That(() => Construct(row, presentation: false),
                Throws.ArgumentException.With.Message.Contains("Row 1 of sector run").And.Message.Contains("doesn't fly 'Bulwark'"));
        }

        [Test]
        public void Session_RefusesAFieldPresetOnARow_WhoseLayoutsFieldNamesItsOwn()
        {
            var row = new SectorEntry { prefab = AssetDatabase.LoadAssetAtPath<Sector>(ArenaPath), field = TrialRow().field };
            Assert.That(() => Construct(row, presentation: false),
                Throws.ArgumentException.With.Message.Contains("Row 1 of sector run").And.Message.Contains("names its own preset"));
        }

        private static Ship TestShip(string name) => AssetDatabase.LoadAssetAtPath<Ship>($"Assets/Prefabs/Ships/{name}.prefab");

        private static SectorPhase[] WithoutGroups(SectorPhase[] phases)
        {
            var copy = new SectorPhase[phases.Length];
            for (var i = 0; i < phases.Length; i++)
            {
                copy[i] = phases[i];
                copy[i].groups = System.Array.Empty<WaveGroup>();
            }
            return copy;
        }

        [Test]
        public void Session_RefusesAPresentedLocalePoolWithAnEmptyEntry()
        {
            var pool = new[] { Locale(OtherLocalePath), null };
            Assert.That(() => Construct(true, pool, NamedRow(), RandomRow()),
                Throws.ArgumentException.With.Message.Contains("an empty entry in its locale pool"));
        }

        [Test]
        public void Session_RefusesAPoolThatCannotGiveEachRandomRowItsOwnLocale_LeavingOutNamedRowLocales()
        {
            var pool = new[] { Locale(LocalePath), Locale(OtherLocalePath) };
            Assert.That(() => Construct(true, pool, NamedRow(), RandomRow(), RandomRow()),
                Throws.ArgumentException.With.Message.Contains("its 2 Random rows").And.Message.Contains("has 1 left"));
        }

        [Test]
        public void SectorResult_Extracted_IsSuccess()
        {
            var result = SectorResult.Extracted(90f);
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.FailReason);
            Assert.AreEqual(90f, result.DepartureHeading);
        }

        [Test]
        public void SectorResult_Failed_HasReason()
        {
            var result = SectorResult.Failed("hull breach");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("hull breach", result.FailReason);
        }

        [Test]
        public void Session_HoldsNoSceneIdentityContainerOrRig()
        {
            var type = typeof(Session);
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(type),
                "Session is a plain object, not a scene component");
            Assert.IsNull(type.GetProperty("Services"),
                "the services are named one by one — a session holds no service container");
            Assert.IsNull(type.GetProperty("Rig"),
                "the player is the host's, not the session's — a session holds no rig");
        }

        [Test]
        public void Session_TakesOnlyItsSubstrateAtConstruction()
        {
            var constructors = typeof(Session).GetConstructors();
            Assert.AreEqual(1, constructors.Length, "Session has a single composition root");
            Assert.AreEqual(4, constructors[0].GetParameters().Length,
                "Session(profile, root, units, objectives) — no rig, no policy");

            Assert.IsNull(typeof(Session).GetProperty("OnSectorComplete"),
                "the sector-complete hook rides the load call, not a settable property");
            Assert.IsNull(typeof(Session).GetProperty("OnPlayerDeath"),
                "the player-death hook belongs to the player rig, not the session");
        }

        [Test]
        public void PlayerRig_DeclaresNoRestartEvent()
        {
            Assert.IsNull(typeof(PlayerRig).GetEvent("RestartRequested"),
                "PlayerRig must not declare a RestartRequested event");
        }
    }
}
