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
using Game.Preview;
using Substrate.Sessions;

namespace Tests.EditMode.Sectors
{
    /// <summary>The shipped run and hangar offer, read as assets: each trial row ends at an elite flown from its own faction and watched by its layout's wave director, sectors 1 and 2 at a 1.5x elite with no escalation, the HUD fixtures at none, and the hangar offers Vanguard as its starter and Crimson locked, since no trial row's elite flies it.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorElitesEditModeTests
    {
        private const string HangarOfferPath = "Assets/Settings/Ships/PlayerLoadout.asset";
        private const string RunPath = "Assets/Settings/Game/SurvivalTrialRun.asset";

        [TestCase(0, "shoal", 1.5f, false)]
        [TestCase(1, "veil", 1.5f, false)]
        [TestCase(2, "bastion", 2f, true)]
        public void TrialRow_EndsAtAnEliteOfItsOwnFaction(int index, string factionId, float size, bool escalates)
        {
            var row = Load<SectorRun>(RunPath).Sectors[index];
            var prefab = row.prefab;
            var encounter = (EliteEncounter)Field(prefab.GetComponent<PhaseSpineModule>(), "elite");
            Assert.IsNotNull(encounter, $"row {index}'s spine owns an elite");
            Assert.AreSame(encounter, Field(prefab.Spawners.OfType<WaveDirector>().Single(), "elite"),
                "the waves stop when the spine's elite arrives");

            var faction = (SectorFaction)Field(encounter, "faction");
            Assert.AreSame(prefab.Module<SectorFaction>(), faction, "the elite flies the sector's own faction");
            Assert.AreEqual(factionId, row.faction.id);

            var profile = row.elite;
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
            Assert.IsNull(PreviewHostBootstrap.FixtureRow(sector).elite, "and its row names none");
        }

        [Test]
        public void HangarOffer_StartsOnVanguard_AndOffersCrimsonThatNoEliteUnlocks()
        {
            var offer = Load<ItemSubset>(HangarOfferPath);
            var crimson = Load<Ship>("Assets/Prefabs/Ships/Crimson.prefab");
            Assert.AreSame(Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab"), offer.starter);
            CollectionAssert.Contains(offer.ships, offer.starter, "the starter is an offered ship");
            CollectionAssert.Contains(offer.ships, crimson, "Crimson is offered, shown locked");
            foreach (var (row, i) in Load<SectorRun>(RunPath).Sectors.Select((row, i) => (row, i)))
                Assert.AreNotSame(crimson, row.faction.EliteShip, $"row {i}'s elite would unlock Crimson");
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
