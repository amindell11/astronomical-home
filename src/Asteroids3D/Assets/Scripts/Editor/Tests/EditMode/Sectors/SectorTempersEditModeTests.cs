using AI.Pilots;
using AI.Tactics;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Factions;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>
    /// Each shipped faction's signature and common ship flies a pilot whose temper is set, and the two
    /// hulls fly different pilots. Both tempers of a faction share one wounded response: Veil never,
    /// Shoal break off, Bastion last stand. No sector prefab names a patrol pilot of its own.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorTempersEditModeTests
    {
        [Test]
        public void EachFactionsSignatureAndCommonShip_HasAPilotWhoseBrainCarriesATemper()
        {
            foreach (var id in new[] { "Shoal", "Veil", "Bastion" })
            {
                var faction = Load(id);
                AssertTemper(faction, faction.signature.ship, $"{id} signature");
                AssertTemper(faction, faction.common.ship, $"{id} common");
            }
        }

        [Test]
        public void OneFactionsTwoHulls_FlyDifferentPilotPrefabs()
        {
            var faction = Load("Shoal");

            Assert.AreNotSame(faction.PilotFor(faction.signature.ship), faction.PilotFor(faction.common.ship));
        }

        [Test]
        public void BothTempersOfAFaction_ShareOneWoundedResponse()
        {
            AssertResponse("Veil", WoundedResponse.Never);
            AssertResponse("Shoal", WoundedResponse.BreakOff);
            AssertResponse("Bastion", WoundedResponse.LastStand);
        }

        [Test]
        public void NoShippedSectorPrefab_SerializesAPatrolPilot()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Sectors" });
            Assert.IsNotEmpty(guids, "the sector prefabs are in the tree");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsTrue(prefab, path);
                foreach (var hunt in prefab.GetComponentsInChildren<SectorHunt>(true))
                {
                    using var serialized = new SerializedObject(hunt);
                    Assert.IsNull(serialized.FindProperty("patrolPilot"), $"{path} serializes a patrol pilot");
                }
            }
        }

        private static void AssertTemper(Faction faction, Ship hull, string label)
        {
            var pilot = faction.PilotFor(hull);
            var brain = pilot.GetComponent<ArchetypeBrain>();
            Assert.IsTrue(brain, $"{label} flies an archetype brain");
            Assert.IsTrue(Temper(pilot), $"{label}'s pilot carries a temper");
        }

        private static void AssertResponse(string id, WoundedResponse response)
        {
            var faction = Load(id);
            Assert.AreEqual(response, Temper(faction.PilotFor(faction.signature.ship)).wounded, $"{id} signature");
            Assert.AreEqual(response, Temper(faction.PilotFor(faction.common.ship)).wounded, $"{id} common");
        }

        private static TacticsSettings Temper(Commander pilot)
        {
            var brain = pilot.GetComponent<ArchetypeBrain>();
            Assert.IsTrue(brain, $"{pilot.name} flies an archetype brain");
            var temper = new SerializedObject(brain).FindProperty("tactics").objectReferenceValue as TacticsSettings;
            Assert.IsTrue(temper, $"{pilot.name} carries a temper");
            return temper;
        }

        private static Faction Load(string id)
        {
            var path = $"Assets/Settings/Factions/{id}.asset";
            var faction = AssetDatabase.LoadAssetAtPath<Faction>(path);
            Assert.IsNotNull(faction, $"asset missing at {path}");
            return faction;
        }
    }
}
