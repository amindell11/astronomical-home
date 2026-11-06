using System.Linq;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Sectors
{
    /// <summary>The shipped sectors and hangar offer, read as assets: each trial sector ends at an elite flown from its own faction and watched by its wave director, sectors 1 and 2 at a 1.5x elite with no escalation, the HUD fixtures at none, and the hangar offers Vanguard as its starter and never Crimson.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorElitesEditModeTests
    {
        private const string HangarOfferPath = "Assets/Settings/Ships/PlayerLoadout.asset";

        [TestCase("TrialSector", "shoal", 1.5f, false)]
        [TestCase("TrialSector2", "veil", 1.5f, false)]
        [TestCase("TrialSector3", "bastion", 2f, true)]
        public void TrialSector_EndsAtAnEliteOfItsOwnFaction(string sector, string factionId, float size, bool escalates)
        {
            var prefab = Load<Sector>($"Assets/Prefabs/Sectors/{sector}.prefab");
            var encounter = (EliteEncounter)Field(prefab.GetComponent<PhaseSpineModule>(), "elite");
            Assert.IsNotNull(encounter, $"{sector}'s spine owns an elite");
            Assert.AreSame(encounter, Field(prefab.Spawners.OfType<WaveDirector>().Single(), "elite"),
                "the waves stop when the spine's elite arrives");

            var faction = (SectorFaction)Field(encounter, "faction");
            Assert.AreSame(prefab.Module<SectorFaction>(), faction, "the elite flies the sector's own faction");
            Assert.AreEqual(factionId, faction.Faction.id);

            var profile = (EliteProfile)Field(encounter, "profile");
            Assert.IsNotNull(profile);
            Assert.DoesNotThrow(profile.Validate);
            Assert.AreEqual(size, profile.Scale.Size);
            Assert.AreEqual(escalates, profile.Escalation.Length > 0);
        }

        [TestCase("HudQuietSector")]
        [TestCase("HudExtractionSector")]
        public void HudFixtureSector_HasNoElite(string sector)
        {
            var prefab = Load<Sector>($"Assets/Prefabs/Sectors/Preview/{sector}.prefab");
            Assert.IsNull(Field(prefab.GetComponent<PhaseSpineModule>(), "elite"));
        }

        [Test]
        public void HangarOffer_StartsOnVanguard_AndOffersCrimsonThatNoEliteUnlocks()
        {
            var offer = Load<ItemSubset>(HangarOfferPath);
            var crimson = Load<Ship>("Assets/Prefabs/Ships/Crimson.prefab");
            Assert.AreSame(Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab"), offer.starter);
            CollectionAssert.Contains(offer.ships, offer.starter, "the starter is an offered ship");
            CollectionAssert.Contains(offer.ships, crimson, "Crimson is offered, shown locked");
            foreach (var sector in new[] { "TrialSector", "TrialSector2", "TrialSector3" })
            {
                var faction = Load<Sector>($"Assets/Prefabs/Sectors/{sector}.prefab").Module<SectorFaction>();
                Assert.AreNotSame(crimson, faction.Faction.EliteShip, $"{sector}'s elite would unlock Crimson");
            }
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"asset missing at {path}");
            return asset;
        }

        private static Object Field(Object owner, string field)
        {
            using var serialized = new SerializedObject(owner);
            var value = serialized.FindProperty(field).objectReferenceValue;
            return value ? value : null;
        }
    }
}
