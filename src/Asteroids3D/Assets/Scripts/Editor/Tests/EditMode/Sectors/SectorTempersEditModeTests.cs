using System.Linq;
using AI.Tactics;
using Game;
using Game.Preview;
using NUnit.Framework;
using RL.Opponents;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Phases;
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

            foreach (var (row, index) in rows.Select((row, index) => (row, index)))
            {
                var sector = row.prefab;
                var label = $"row {index} ({sector.name})";
                var ships = row.phases.SelectMany(phase => phase.groups ?? System.Array.Empty<WaveGroup>())
                    .SelectMany(group => group.ships ?? System.Array.Empty<WaveDirector.RosterEntry>())
                    .ToArray();
                Assert.That(ships, Is.Not.Empty, $"{label} has a wave ship");
                for (var i = 0; i < ships.Length; i++)
                    AssertTemper("Wave", ships[i].pilot, $"{label}'s wave ship {i}");

                AssertTemper("Wave", Field(sector.GetComponentInChildren<SectorHunt>(true), "patrolPilot"),
                    $"{label}'s patrols");
                var elite = sector.GetComponentInChildren<EliteEncounter>(true);
                AssertTemper("Elite", Field(elite, "pilot"), $"{label}'s elite");
                AssertTemper("Escort", Field(elite, "escortPilot"), $"{label}'s escorts");
            }
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
