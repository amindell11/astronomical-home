using System.Linq;
using AI.Tactics;
using Game;
using Game.Preview;
using NUnit.Framework;
using RL.Opponents;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Hunt;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Sectors
{
    [TestFixture]
    [Category("Sectors")]
    public class SectorTempersEditModeTests
    {
        [Test]
        public void EveryShippedRow_AndTheCrowdedFightRow_FlyEachKindOfShipOnItsTemper()
        {
            var rows = Load<GameHost>(PreviewHostBootstrap.GameHostPath).sessionProfile.run.Sectors
                .Append(PreviewHostBootstrap.FixtureRow("CrowdedFightSector"))
                .ToArray();
            Assert.That(rows, Has.Length.GreaterThan(1), "the run ships sectors");
            var waveTempers = new[] { "Aggressor", "Orbiter", "Kiter", "Lurker" }
                .Select(kind => Load<TacticsSettings>($"Assets/Settings/AI/Tactics/{kind}Temper.asset"))
                .ToArray();

            foreach (var (row, index) in rows.Select((row, index) => (row, index)))
            {
                var sector = row.prefab;
                var label = $"row {index} ({sector.name})";
                var ships = row.phases.SelectMany(phase => phase.groups).SelectMany(group => group.ships).ToArray();
                Assert.That(ships, Is.Not.Empty, $"{label} has a wave ship");
                for (var i = 0; i < ships.Length; i++)
                    AssertOneOf(waveTempers, ships[i].pilot, $"{label}'s wave ship {i}");

                AssertTemper("Aggressor", Field(sector.GetComponentInChildren<SectorHunt>(true), "patrolPilot"),
                    $"{label}'s patrols");
                var elite = sector.GetComponentInChildren<EliteEncounter>(true);
                AssertTemper("Elite", Field(elite, "pilot"), $"{label}'s elite");
                AssertTemper("Escort", Field(elite, "escortPilot"), $"{label}'s escorts");
            }
        }

        [Test]
        public void WavePilots_AreFourDifferentPilots()
        {
            var tempers = new[] { "Aggressor", "Orbiter", "Kiter", "Lurker" }
                .Select(kind => TemperOf($"Assets/Prefabs/Pilots/{kind}Pilot.prefab"))
                .ToArray();

            Assert.AreEqual(4, tempers.Distinct().Count(), "four different temper assets");
            for (var i = 0; i < tempers.Length; i++)
                for (var j = i + 1; j < tempers.Length; j++)
                    Assert.IsFalse(tempers[i].home == tempers[j].home
                        && tempers[i].attackRunMargin == tempers[j].attackRunMargin,
                        "no two wave pilots are the same pilot");
        }

        [Test]
        public void ShippedRun_FliesALurker()
        {
            const string lurker = "Assets/Prefabs/Pilots/LurkerPilot.prefab";
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath(lurker), $"asset missing at {lurker}");
            var rows = Load<GameHost>(PreviewHostBootstrap.GameHostPath).sessionProfile.run.Sectors;
            Assert.IsTrue(rows.SelectMany(row => row.phases)
                .SelectMany(phase => phase.groups)
                .SelectMany(group => group.ships)
                .Any(ship => ship.pilot && AssetDatabase.GetAssetPath(ship.pilot) == lurker),
                "at least one wave ship flies LurkerPilot");
        }

        private static void AssertOneOf(TacticsSettings[] tempers, Object pilot, string ships)
        {
            Assert.IsNotNull(pilot, $"{ships} have a pilot");
            var brain = ((Component)pilot).GetComponent<ArchetypeBrain>();
            Assert.IsNotNull(brain, $"{ships} fly an archetype brain");
            var temper = Field(brain, "tactics") as TacticsSettings;
            Assert.IsTrue(temper && tempers.Any(candidate => candidate == temper), $"{ships} fly a wave temper");
        }

        private static TacticsSettings TemperOf(string path)
        {
            var pilot = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            Assert.IsNotNull(pilot, $"Missing prefab: {path}");
            var temper = Field(pilot.GetComponent<ArchetypeBrain>(), "tactics") as TacticsSettings;
            Assert.IsNotNull(temper, $"{path} names a temper");
            return temper;
        }

        private static void AssertTemper(string kind, Object pilot, string ships)
        {
            Assert.IsNotNull(pilot, $"{ships} have a pilot");
            var brain = ((Component)pilot).GetComponent<ArchetypeBrain>();
            Assert.IsNotNull(brain, $"{ships} fly an archetype brain");
            Assert.AreSame(Load<TacticsSettings>($"Assets/Settings/AI/Tactics/{kind}Temper.asset"), Field(brain, "tactics"),
                $"{ships} fly the {kind.ToLowerInvariant()} temper");
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"asset missing at {path}");
            return asset;
        }

        private static Object Field(Object owner, string field)
        {
            Assert.IsNotNull(owner, $"no component to read '{field}' from");
            using var serialized = new SerializedObject(owner);
            var value = serialized.FindProperty(field).objectReferenceValue;
            return value ? value : null;
        }
    }
}
