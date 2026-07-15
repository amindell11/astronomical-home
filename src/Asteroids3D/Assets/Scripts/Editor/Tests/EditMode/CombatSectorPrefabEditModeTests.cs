using System.Linq;
using Game;
using Game.Sectors;
using NUnit.Framework;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the authored CombatSector.prefab wiring: present-at-spawn fixtures, spine module refs, and the port-identity signal chain.</summary>
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
        public void Modules_AreSpineGateChaserActivateThenAmbush()
        {
            var sector = LoadSector();
            Assert.AreEqual(6, sector.Modules.Count);
            Assert.IsInstanceOf<SectorSpineModule>(sector.Modules[0]);
            Assert.IsInstanceOf<TriggerVolume>(sector.Modules[1]);
            Assert.IsInstanceOf<ExtractionChallengeRule>(sector.Modules[2]);
            Assert.IsInstanceOf<ActivateOnSignal>(sector.Modules[3]);
            Assert.IsInstanceOf<TriggerVolume>(sector.Modules[4]);
            Assert.IsInstanceOf<AmbushEncounter>(sector.Modules[5]);
        }

        [Test]
        public void SignalGraph_ValidatesClean()
        {
            var model = SectorSignalGraph.Build(LoadSector());
            var errors = model.Findings
                .Where(f => f.Severity == SectorSignalGraph.Severity.Error)
                .Select(f => f.Message).ToList();
            Assert.IsEmpty(errors, "The shipped demo sector must carry zero validator errors.");
        }

        [Test]
        public void Ambush_WiresAreaPortToDelayedRule_AndGatedWave()
        {
            var sector = LoadSector();
            var volume = (TriggerVolume)sector.Modules[4];
            var ambush = (AmbushEncounter)sector.Modules[5];

            var area = volume.InsidePort;
            Assert.IsNotNull(area, "The ambush trigger must own an inside port.");
            Assert.AreEqual(SignalPort.PortKind.Level, area.Kind);
            Assert.AreSame(volume.gameObject, area.gameObject,
                "The inside port must sit on the trigger that publishes it.");

            Assert.AreEqual(1, ambush.Terms.Count, "The ambush must gate on the area port only.");
            Assert.AreEqual(ActivationTerm.TermKind.Signal, ambush.Terms[0].kind);
            Assert.AreSame(area, ambush.Terms[0].signal,
                "The ambush term must reference the exact port the trigger publishes — identity, not a name.");
            Assert.Greater(new SerializedObject(ambush).FindProperty("fireDelaySeconds").floatValue, 0f,
                "The ambush must fire on a delay after the area is entered.");

            Assert.IsNotNull(ambush.FiredPort);
            Assert.AreEqual(SignalPort.PortKind.Latch, ambush.FiredPort.Kind);
            Assert.IsNotNull(ambush.ClearedPort);
            Assert.AreEqual(SignalPort.PortKind.Latch, ambush.ClearedPort.Kind);
            Assert.AreNotSame(ambush.FiredPort, ambush.ClearedPort,
                "Fired and cleared are distinct signals and must be distinct ports.");

            Assert.AreEqual(2, sector.Spawners.Count);
            var wave = sector.Spawners[1] as RingSpawner;
            Assert.IsNotNull(wave, "The ambush wave must be the second authored spawner.");
            Assert.AreSame(wave, new SerializedObject(ambush).FindProperty("waveSpawner").objectReferenceValue,
                "The rule must observe its own wave spawner.");
            Assert.AreEqual(SectorSpawner.ActivationMode.Gated, wave.Mode, "The wave must be explicitly Gated.");
            Assert.AreSame(ambush.FiredPort, wave.ActivationSignal,
                "The wave must be gated on the exact port the rule latches.");
            Assert.IsTrue(volume.transform.IsChildOf(ambush.transform),
                "Hierarchy = ownership: the encounter owns its trigger.");
            Assert.IsTrue(wave.transform.IsChildOf(ambush.transform),
                "Hierarchy = ownership: the encounter owns its wave.");
            Assert.AreEqual((int)RespawnPolicy.Origin.None,
                new SerializedObject(wave).FindProperty("respawn").FindPropertyRelative("origin").enumValueIndex,
                "Wave ships must not respawn.");
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
            Assert.Less((keyPlane - new Vector2(-108.5f, 154.1f)).magnitude, 0.01f,
                $"Key must sit at the authored plane position (-108.5,154.1), was {keyPlane}.");
            Assert.Less((gatePlane - new Vector2(173.9f, 170.6f)).magnitude, 0.01f,
                $"Gate must sit at the authored plane position (173.9,170.6), was {gatePlane}.");
        }

        [Test]
        public void SpineModule_OwnsFiveDistinctLatchStepPorts_OnTheSectorRoot()
        {
            var sector = LoadSector();
            var spine = (SectorSpineModule)sector.Modules[0];

            var ports = spine.StepPorts.ToList();
            Assert.AreEqual(5, ports.Count);
            foreach (var (step, port) in ports)
            {
                Assert.IsNotNull(port, $"Spine step '{step}' must have an authored port.");
                Assert.AreEqual(SignalPort.PortKind.Latch, port.Kind, $"Spine step '{step}' must be a latch.");
                Assert.AreSame(sector.gameObject, port.gameObject,
                    $"Spine step port '{step}' must sit on the spine module's own GameObject.");
            }
            Assert.AreEqual(5, ports.Select(p => p.Port).Distinct().Count(),
                "Copy/paste hazard: each spine step must reference a distinct port.");
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
            Assert.IsNotNull(volume.InsidePort, "The gate volume must own an inside port.");
            Assert.AreEqual(SignalPort.PortKind.Level, volume.InsidePort.Kind);
            Assert.AreSame(zone.gameObject, volume.InsidePort.gameObject);
        }

        [Test]
        public void ExtractionRule_GatesOnReadyToExtractPort_AndOwnsAFiredPort()
        {
            var sector = LoadSector();
            var spine = (SectorSpineModule)sector.Modules[0];
            var rule = (ExtractionChallengeRule)sector.Modules[2];

            var readyToExtract = spine.StepPorts
                .First(p => p.Step == SectorSpineModule.StepReadyToExtract).Port;

            Assert.AreEqual(1, rule.Terms.Count, "The thin rule must gate on the spine port only.");
            Assert.AreEqual(ActivationTerm.TermKind.Signal, rule.Terms[0].kind);
            Assert.AreSame(readyToExtract, rule.Terms[0].signal,
                "The rule must reference the exact ready-to-extract port the spine latches.");

            Assert.IsNotNull(rule.FiredPort, "The rule must own a fired port.");
            Assert.AreEqual(SignalPort.PortKind.Latch, rule.FiredPort.Kind);
            Assert.AreSame(rule.gameObject, rule.FiredPort.gameObject);

            var zone = new SerializedObject(sector.Modules[0]).FindProperty("extractionZone").objectReferenceValue;
            Assert.AreSame(zone, new SerializedObject(rule).FindProperty("extractionZone").objectReferenceValue,
                "The rule must bind the same zone fixture as the spine module.");
        }

        [Test]
        public void ChaserActivate_ListensToTheRulesFiredPort_OnTheDormantAdoptedChaser()
        {
            var sector = LoadSector();
            var rule = (ExtractionChallengeRule)sector.Modules[2];
            var activate = (ActivateOnSignal)sector.Modules[3];

            Assert.AreSame(rule.FiredPort, activate.Signal,
                "The chaser's activate module must listen to the exact port the rule latches.");

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
