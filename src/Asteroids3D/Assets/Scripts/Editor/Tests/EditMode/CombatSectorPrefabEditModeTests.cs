using Game;
using Game.Sectors;
using NUnit.Framework;
using Objectives;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the authored CombatSector.prefab wiring: present-at-spawn fixtures, spine module refs, and the thin extraction rule.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class CombatSectorPrefabEditModeTests
    {
        private const string PrefabPath = "Assets/Prefabs/Sectors/CombatSector.prefab";

        private Sector LoadSector()
        {
            var sector = AssetDatabase.LoadAssetAtPath<Sector>(PrefabPath);
            Assert.IsNotNull(sector, $"CombatSector prefab missing at {PrefabPath}");
            return sector;
        }

        [Test]
        public void Manifest_MatchesHierarchy_NoDrift()
        {
            Assert.IsFalse(LoadSector().ComputeDrift().HasDrift,
                "The baked manifest must match what the Sync crawl collects from the hierarchy.");
        }

        [Test]
        public void Modules_AreSpineThenGateVolumeThenRuleThenChaserActivate()
        {
            var sector = LoadSector();
            Assert.AreEqual(4, sector.Modules.Count);
            Assert.IsInstanceOf<SectorSpineModule>(sector.Modules[0]);
            Assert.IsInstanceOf<TriggerVolume>(sector.Modules[1]);
            Assert.IsInstanceOf<ExtractionChallengeRule>(sector.Modules[2]);
            Assert.IsInstanceOf<ActivateOnToken>(sector.Modules[3]);
        }

        [Test]
        public void SpineModule_BindsAuthoredFixtures_AtAuthoredPlanePositions()
        {
            var sector = LoadSector();
            var spine = new SerializedObject(sector.Modules[0]);

            var key = spine.FindProperty("keyPickup").objectReferenceValue as KeyPickup;
            var zone = spine.FindProperty("extractionZone").objectReferenceValue as ExtractionZone;
            Assert.IsNotNull(key, "SpineModule.keyPickup must resolve to the authored KeyPickup fixture.");
            Assert.IsNotNull(zone, "SpineModule.extractionZone must resolve to the authored ExtractionZone fixture.");
            Assert.IsTrue(key.transform.IsChildOf(sector.transform), "The key must be a present-at-spawn sector child.");
            Assert.IsTrue(zone.transform.IsChildOf(sector.transform), "The gate must be a present-at-spawn sector child.");

            var keyPlane = GamePlane.WorldPointToPlane(key.transform.position);
            var gatePlane = GamePlane.WorldPointToPlane(zone.transform.position);
            Assert.Less((keyPlane - new Vector2(-25f, 50f)).magnitude, 0.01f,
                $"Key must sit at the old authored plane position (-25,50), was {keyPlane}.");
            Assert.Less((gatePlane - new Vector2(50f, 50f)).magnitude, 0.01f,
                $"Gate must sit at the old authored plane position (50,50), was {gatePlane}.");
        }

        [Test]
        public void Gate_CarriesZoneVolumeAndRule_OnOneFixture()
        {
            var sector = LoadSector();
            var zone = new SerializedObject(sector.Modules[0]).FindProperty("extractionZone")
                .objectReferenceValue as ExtractionZone;
            var volume = (TriggerVolume)sector.Modules[1];
            var rule = (ExtractionChallengeRule)sector.Modules[2];

            Assert.AreSame(zone.gameObject, volume.gameObject);
            Assert.AreSame(zone.gameObject, rule.gameObject);
            Assert.AreEqual("in-gate", new SerializedObject(volume).FindProperty("signalToken").stringValue);
        }

        [Test]
        public void ExtractionRule_GatesOnReadyToExtract_AndPublishesChallengeToken()
        {
            var sector = LoadSector();
            var rule = new SerializedObject(sector.Modules[2]);

            var terms = rule.FindProperty("terms");
            Assert.AreEqual(1, terms.arraySize, "The thin rule must gate on the spine token only.");
            var term = terms.GetArrayElementAtIndex(0);
            Assert.AreEqual((int)ActivationTerm.TermKind.Signal, term.FindPropertyRelative("kind").enumValueIndex);
            Assert.AreEqual(SectorSpineModule.TokenPrefix + SectorSpineModule.StepReadyToExtract,
                term.FindPropertyRelative("signalToken").stringValue);

            var published = rule.FindProperty("publishOnFired");
            Assert.AreEqual(1, published.arraySize, "The rule must publish the challenge-started token.");
            Assert.AreEqual("extraction-challenge-started", published.GetArrayElementAtIndex(0).stringValue);

            var zone = new SerializedObject(sector.Modules[0]).FindProperty("extractionZone").objectReferenceValue;
            Assert.AreSame(zone, rule.FindProperty("extractionZone").objectReferenceValue,
                "The rule must bind the same zone fixture as the spine module.");
        }

        [Test]
        public void ChaserActivate_ListensForChallengeToken_OnTheDormantAdoptedChaser()
        {
            var sector = LoadSector();
            var activate = (ActivateOnToken)sector.Modules[3];

            Assert.AreEqual("extraction-challenge-started",
                new SerializedObject(activate).FindProperty("token").stringValue,
                "The chaser's activate module must listen for the token the rule publishes.");

            Assert.AreEqual(2, sector.Adopted.Count);
            var chaser = sector.Adopted[0].target as Ship;
            Assert.IsNotNull(chaser, "The first adopted ship must be the chaser.");
            Assert.AreSame(chaser.gameObject, activate.gameObject,
                "The activate module must sit on the chaser itself — the actee subscribes.");
            Assert.IsFalse(sector.Adopted[0].startActive, "The chaser must be adopted dormant (startActive=false).");
        }

        [Test]
        public void ExtractionZone_BindsChaserAsSerializedBlocker()
        {
            var sector = LoadSector();
            var zone = new SerializedObject(sector.Modules[0]).FindProperty("extractionZone")
                .objectReferenceValue as ExtractionZone;
            var chaser = sector.Adopted[0].target as Ship;

            Assert.AreSame(chaser.transform, new SerializedObject(zone).FindProperty("blocker").objectReferenceValue,
                "The zone must observe the chaser as its serialized blocker.");
        }
    }
}
