using System.Collections;
using System.Collections.Generic;
using Game.Sectors;
using NUnit.Framework;
using Objectives;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>TriggerVolume occupancy mirrored to the sector bus, compound-collider occupancy, and the parked-then-qualified activation scenario end-to-end (real physics triggers).</summary>
    [TestFixture]
    [Category("Sectors")]
    public class TriggerVolumeActivationPlayModeTests : PlayModeWorldFixture
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
            base.TearDown();
        }

        private GameObject TrackGO(GameObject go)
        {
            _created.Add(go);
            return go;
        }

        private TriggerVolume CreateVolume(Rigidbody player = null, float radius = 3f)
        {
            var go = TrackGO(new GameObject("TriggerVolume"));
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            var volume = go.AddComponent<TriggerVolume>();
            volume.Configure(player);
            return volume;
        }

        private static SignalRef Inside(TriggerVolume volume) =>
            new SignalRef(volume, TriggerVolume.OutputInside);

        /// <summary>Rigidbody on the root, collider(s) on children — mirrors real player ships.</summary>
        private Rigidbody CreatePlayerBody(Vector3 pos, int colliderCount = 1)
        {
            var root = TrackGO(new GameObject("PlayerRoot"));
            root.transform.position = pos;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            for (var i = 0; i < colliderCount; i++)
            {
                var child = new GameObject($"HullCollider{i}");
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(i * 2f - (colliderCount - 1), 0f, 0f);
                child.AddComponent<SphereCollider>().radius = 0.5f;
            }
            return rb;
        }

        [UnityTest]
        public IEnumerator TriggerVolume_PlayerEnterExit_MirrorsBusLevel()
        {
            var player = CreatePlayerBody(new Vector3(100f, 0f, 0f));
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var volume = CreateVolume(player);
            yield return volume.Setup(ctx);

            Assert.IsFalse(bus.Get(Inside(volume)));

            player.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(bus.Get(Inside(volume)), "Player entering the volume must raise the bus level.");

            player.transform.position = new Vector3(100f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(bus.Get(Inside(volume)), "Player exiting the volume must clear the bus level.");

            yield return volume.Teardown(ctx);
        }

        [UnityTest]
        public IEnumerator TriggerVolume_TriggerBeforeSetup_PushesBufferedLevelOnSetup()
        {
            var player = CreatePlayerBody(Vector3.zero);
            var volume = CreateVolume(player);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            Assert.IsFalse(bus.Get(Inside(volume)));

            yield return volume.Setup(ctx);
            Assert.IsTrue(bus.Get(Inside(volume)),
                "Occupancy accumulated before Setup must be pushed onto the bus when Setup wires it.");

            yield return volume.Teardown(ctx);
        }

        [UnityTest]
        public IEnumerator TriggerVolume_CompoundCollider_StraddlingExit_KeepsLevel()
        {
            var player = CreatePlayerBody(new Vector3(100f, 0f, 0f), colliderCount: 2);
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var volume = CreateVolume(player, radius: 3f);
            yield return volume.Setup(ctx);

            player.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(bus.Get(Inside(volume)), "Both colliders inside must read as in.");

            player.transform.position = new Vector3(2.7f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(bus.Get(Inside(volume)),
                "A compound collider straddling the boundary must still read as inside — occupancy is per rigidbody, not per collider.");

            player.transform.position = new Vector3(100f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(bus.Get(Inside(volume)), "All colliders out must clear the level.");

            yield return volume.Teardown(ctx);
        }

        [UnityTest]
        public IEnumerator ExtractionZone_CompoundCollider_StraddlingExit_StaysInZone()
        {
            var player = CreatePlayerBody(new Vector3(100f, 0f, 0f), colliderCount: 2);
            var zoneGO = TrackGO(new GameObject("Zone"));
            var col = zoneGO.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 3f;
            var zone = zoneGO.AddComponent<ExtractionZone>();
            zone.BindPlayer(player);
            zone.Arm();

            player.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(zone.IsPlayerInZone);

            player.transform.position = new Vector3(2.7f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(zone.IsPlayerInZone,
                "A compound collider straddling the zone boundary must still count as in-zone.");

            player.transform.position = new Vector3(100f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(zone.IsPlayerInZone);
        }

        [UnityTest]
        public IEnumerator TriggerVolume_PlayerDisabledInside_ClearsBusLevel_AndReactivationRedetects()
        {
            var player = CreatePlayerBody(new Vector3(100f, 0f, 0f));
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var volume = CreateVolume(player);
            yield return volume.Setup(ctx);

            player.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(bus.Get(Inside(volume)));

            player.gameObject.SetActive(false);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(bus.Get(Inside(volume)),
                "A player deactivated inside fires no exit event — the republished pruned level must still clear.");

            player.gameObject.SetActive(true);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(bus.Get(Inside(volume)),
                "Reactivating the player inside must re-detect via a fresh enter event.");

            yield return volume.Teardown(ctx);
        }

        [UnityTest]
        public IEnumerator ExtractionZone_PlayerDisabledInside_ReadsNotInZone_AndReactivationRedetects()
        {
            var player = CreatePlayerBody(new Vector3(100f, 0f, 0f));
            var zoneGO = TrackGO(new GameObject("Zone"));
            var col = zoneGO.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 3f;
            var zone = zoneGO.AddComponent<ExtractionZone>();
            zone.BindPlayer(player);
            zone.Arm();

            player.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(zone.IsPlayerInZone);

            player.gameObject.SetActive(false);
            Assert.IsFalse(zone.IsPlayerInZone,
                "A player deactivated inside fires no exit event — the lazy query must prune it immediately.");

            player.gameObject.SetActive(true);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(zone.IsPlayerInZone, "Reactivating the player inside must re-detect.");
        }

        [UnityTest]
        public IEnumerator ExtractionZone_Unarmed_NeverReadsInZone()
        {
            var player = CreatePlayerBody(new Vector3(100f, 0f, 0f));
            var zoneGO = TrackGO(new GameObject("Zone"));
            var col = zoneGO.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 3f;
            var zone = zoneGO.AddComponent<ExtractionZone>();
            zone.BindPlayer(player);

            player.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(zone.IsPlayerInZone, "An unarmed zone must read as not-in-zone even with the player inside.");

            zone.Arm();
            Assert.IsTrue(zone.IsPlayerInZone, "Arming must expose the already-buffered occupancy.");

            zone.Disarm();
            Assert.IsFalse(zone.IsPlayerInZone, "Disarming must gate the zone again.");
        }

        [UnityTest]
        public IEnumerator TimeRule_OnFrozenBus_NeverFires_WhileLiveBusRuleFires()
        {
            var frozenBus = new SectorEventBus();
            var liveBus = new SectorEventBus();

            var frozenRule = TrackGO(new GameObject("FrozenTimeRule")).AddComponent<ActivationRule>();
            frozenRule.Configure(new[] { ActivationTerm.Time(0.05f) });
            var liveRule = TrackGO(new GameObject("LiveTimeRule")).AddComponent<ActivationRule>();
            liveRule.Configure(new[] { ActivationTerm.Time(0.05f) });

            yield return frozenRule.Setup(new SectorBuildContext(null, null, null, frozenBus));
            yield return liveRule.Setup(new SectorBuildContext(null, null, null, liveBus));
            frozenBus.Freeze();

            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(liveRule.HasFired, "The live-bus time rule must fire past its threshold (control).");
            Assert.IsFalse(frozenRule.HasFired, "No rule may fire once its bus is frozen.");
        }

        [UnityTest]
        public IEnumerator ParkedThenQualified_RuleFires_WhenLatchedTermArrivesWhileParkedInside()
        {
            var player = CreatePlayerBody(new Vector3(100f, 0f, 0f));
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);

            var volume = CreateVolume(player);
            var keyDonor = TrackGO(new GameObject("KeyDonor")).AddComponent<ActivationRule>();
            var keyAcquired = new SignalRef(keyDonor, ActivationRule.OutputFired);
            var rule = TrackGO(new GameObject("ExtractionRule")).AddComponent<ActivationRule>();
            rule.Configure(new[]
            {
                ActivationTerm.Signal(Inside(volume)),
                ActivationTerm.Signal(keyAcquired)
            });

            yield return volume.Setup(ctx);
            yield return rule.Setup(ctx);

            var fired = 0;
            rule.Fired += () => fired++;

            player.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(bus.Get(Inside(volume)));
            Assert.AreEqual(0, fired, "Parked in the gate without the key must not fire the rule.");

            bus.Latch(keyAcquired);
            Assert.AreEqual(1, fired,
                "The rule must fire for a player already parked inside when the latched term arrives — no enter-edge needed.");
            Assert.IsTrue(bus.Get(new SignalRef(rule, ActivationRule.OutputFired)),
                "Firing must latch the rule's fired output.");

            player.transform.position = new Vector3(100f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            player.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(1, fired, "Leaving and re-entering the volume must not re-fire a latched rule.");

            yield return rule.Teardown(ctx);
            yield return volume.Teardown(ctx);
        }
    }
}
