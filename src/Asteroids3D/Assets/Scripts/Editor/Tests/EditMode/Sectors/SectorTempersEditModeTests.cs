using System.Linq;
using AI.Tactics;
using Game;
using Game.Preview;
using NUnit.Framework;
using RL.Opponents;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Hunt;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Sectors
{
    /// <summary>The shipped run's sectors and the crowded fight, read as assets with every variant override applied:
    /// roster ships and hunt patrols fly the wave temper, the elite the elite temper, its escorts the escort temper.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorTempersEditModeTests
    {
        private const string CrowdedFightPath = "Assets/Prefabs/Sectors/Preview/CrowdedFightSector.prefab";

        [Test]
        public void EveryShippedSector_AndTheCrowdedFight_FlyEachKindOfShipOnItsTemper()
        {
            var sectors = Load<GameHost>(PreviewHostBootstrap.GameHostPath).sessionProfile.run.Sectors
                .Select(row => row.prefab)
                .Append(Load<Sector>(CrowdedFightPath))
                .ToArray();
            Assert.That(sectors, Has.Length.GreaterThan(1), "the run ships sectors");

            foreach (var sector in sectors)
            {
                using var director = new SerializedObject(sector.Spawners.OfType<WaveDirector>().Single());
                var roster = director.FindProperty("roster");
                Assert.That(roster.arraySize, Is.GreaterThan(0), $"{sector.name} has a roster");
                for (var i = 0; i < roster.arraySize; i++)
                    AssertTemper("Wave", roster.GetArrayElementAtIndex(i).FindPropertyRelative("pilot").objectReferenceValue,
                        $"{sector.name}'s roster ship {i}");

                AssertTemper("Wave", Field(sector.GetComponentInChildren<SectorHunt>(true), "patrolPilot"),
                    $"{sector.name}'s patrols");
                var elite = sector.GetComponentInChildren<EliteEncounter>(true);
                AssertTemper("Elite", Field(elite, "pilot"), $"{sector.name}'s elite");
                AssertTemper("Escort", Field(elite, "escortPilot"), $"{sector.name}'s escorts");
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
