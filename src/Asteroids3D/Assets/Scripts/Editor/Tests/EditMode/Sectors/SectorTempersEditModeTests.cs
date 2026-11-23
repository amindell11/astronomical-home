using AI.Pilots;
using AI.Tactics;
using NUnit.Framework;
using Ships.Command;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Factions;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>
    /// Each shipped faction's rival and minion fly pilots whose tempers are set, and the two ranks fly
    /// different pilots. The minion flies with no shield on its faction's hull factor, at 0.75 size only
    /// while it shares the rival's chassis (its own chassis carries its size), and its pilot never
    /// fires the keystone. Both tempers of a faction share one wounded response: Veil never, Shoal jump
    /// out, Bastion last stand. Only Veil's minion regroups when its leader dies. No sector prefab names a
    /// patrol pilot of its own.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorTempersEditModeTests
    {
        [Test]
        public void EachFactionsRivalAndMinion_HasAPilotWhoseBrainCarriesATemper()
        {
            foreach (var id in new[] { "Shoal", "Veil", "Bastion" })
            {
                var faction = Load(id);
                AssertTemper(faction.Rival.pilot, $"{id} rival");
                AssertTemper(faction.Minion.pilot, $"{id} minion");
                var ownChassis = faction.Minion.ship != faction.Rival.ship;
                Assert.AreEqual(ownChassis ? 1f : 0.75f, faction.Minion.scale.Size, 1e-4f, $"{id} minion size");
                Assert.AreEqual(MinionHull(id), faction.Minion.scale.Hull, 1e-4f, $"{id} minion hull");
                Assert.AreEqual(0f, faction.Minion.scale.Shield, 1e-4f, $"{id} minion shield");
                Assert.AreEqual($"Assets/Prefabs/Pilots/Factions/{id}MinionPilot.prefab",
                    AssetDatabase.GetAssetPath(faction.Minion.pilot), id);
                Assert.AreEqual($"Assets/Settings/AI/Tactics/Factions/{id}MinionTemper.asset",
                    AssetDatabase.GetAssetPath(Temper(faction.Minion.pilot)), id);
                Assert.IsTrue(new SerializedObject(faction.Minion.pilot).FindProperty("suppressKeystone").boolValue,
                    $"{id} minion holds its keystone");
            }
        }

        [Test]
        public void OneFactionsTwoRanks_FlyDifferentPilotPrefabs()
        {
            var faction = Load("Shoal");

            Assert.AreNotSame(faction.Rival.pilot, faction.Minion.pilot);
            Assert.IsTrue(faction.Minion.ship, "the minion names its chassis");
        }

        [Test]
        public void BothTempersOfAFaction_ShareOneWoundedResponse()
        {
            AssertResponse("Veil", WoundedResponse.Never);
            AssertResponse("Shoal", WoundedResponse.JumpOut);
            AssertResponse("Bastion", WoundedResponse.LastStand);
        }

        [Test]
        public void OnlyVeilsMinion_RegroupsWithoutItsLeader()
        {
            foreach (var id in new[] { "Shoal", "Veil", "Bastion" })
            {
                var faction = Load(id);
                Assert.AreEqual(id == "Veil", Temper(faction.Minion.pilot).regroupsWithoutLeader, $"{id} minion");
                Assert.IsFalse(Temper(faction.Rival.pilot).regroupsWithoutLeader, $"{id} rival");
            }
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

        // Two laser bolts kill any faction's minion; Bastion's chassis carries more hull.
        internal static float MinionHull(string faction) => faction switch
        {
            "Shoal" or "Veil" => 0.45f,
            "Bastion" => 0.24f,
            _ => throw new System.ArgumentOutOfRangeException(nameof(faction), faction, "no shipped faction"),
        };

        private static void AssertTemper(Commander pilot, string label)
        {
            var brain = pilot.GetComponent<ArchetypeBrain>();
            Assert.IsTrue(brain, $"{label} flies an archetype brain");
            Assert.IsTrue(Temper(pilot), $"{label}'s pilot carries a temper");
        }

        private static void AssertResponse(string id, WoundedResponse response)
        {
            var faction = Load(id);
            Assert.AreEqual(response, Temper(faction.Rival.pilot).wounded, $"{id} rival");
            Assert.AreEqual(response, Temper(faction.Minion.pilot).wounded, $"{id} minion");
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
