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
        public void Modules_AreSpineThenGateVolumeThenRule()
        {
            var sector = LoadSector();
            Assert.AreEqual(3, sector.Modules.Count);
            Assert.IsInstanceOf<SectorSpineModule>(sector.Modules[0]);
            Assert.IsInstanceOf<TriggerVolume>(sector.Modules[1]);
            Assert.IsInstanceOf<ExtractionChallengeRule>(sector.Modules[2]);
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
        public void ExtractionRule_GatesOnReadyToExtract_AndBindsChaserAndZone()
        {
            var sector = LoadSector();
            var rule = new SerializedObject(sector.Modules[2]);

            var terms = rule.FindProperty("terms");
            Assert.AreEqual(1, terms.arraySize, "The thin rule must gate on the spine token only.");
            var term = terms.GetArrayElementAtIndex(0);
            Assert.AreEqual((int)ActivationTerm.TermKind.Signal, term.FindPropertyRelative("kind").enumValueIndex);
            Assert.AreEqual(SectorSpineModule.TokenPrefix + SectorSpineModule.StepReadyToExtract,
                term.FindPropertyRelative("signalToken").stringValue);
            Assert.AreEqual(0, rule.FindProperty("publishOnFired").arraySize);

            var zone = new SerializedObject(sector.Modules[0]).FindProperty("extractionZone").objectReferenceValue;
            Assert.AreSame(zone, rule.FindProperty("extractionZone").objectReferenceValue,
                "The rule must bind the same zone fixture as the spine module.");

            var chaser = rule.FindProperty("chaser").objectReferenceValue as Ship;
            Assert.IsNotNull(chaser, "The rule must bind the prefab-internal chaser ship.");
            Assert.AreEqual(2, sector.Adopted.Count);
            Assert.AreSame(chaser, sector.Adopted[0].target,
                "The chaser must be the dormant adopted ship (startActive=false).");
        }
    }
}
