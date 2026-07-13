using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Sectors;
using NUnit.Framework;
using Player;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>PlayMode tests for TriggerVolume occupancy mirrored to the sector bus and the parked-then-qualified activation scenario end-to-end (real physics triggers).</summary>
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

        private TriggerVolume CreateVolume(string token, float radius = 3f)
        {
            var go = TrackGO(new GameObject("TriggerVolume"));
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            var volume = go.AddComponent<TriggerVolume>();
            volume.Configure(token);
            return volume;
        }

        /// <summary>Root with PlayerMarker, collider + kinematic rigidbody on a child — mirrors real player ships.</summary>
        private GameObject CreatePlayer(Vector3 pos)
        {
            var root = TrackGO(new GameObject("PlayerRoot"));
            root.transform.position = pos;
            root.AddComponent<PlayerMarker>();

            var child = new GameObject("HullCollider");
            child.transform.SetParent(root.transform, false);
            var rb = child.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            child.AddComponent<SphereCollider>().radius = 0.5f;
            return root;
        }

        [UnityTest]
        public IEnumerator TriggerVolume_PlayerEnterExit_MirrorsBusLevel()
        {
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var volume = CreateVolume("in-zone");
            yield return volume.Setup(ctx);

            Assert.IsFalse(bus.Get("in-zone"));

            var player = CreatePlayer(Vector3.zero);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(bus.Get("in-zone"), "Player entering the volume must raise the bus level.");

            player.transform.position = new Vector3(100f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(bus.Get("in-zone"), "Player exiting the volume must clear the bus level.");

            yield return volume.Teardown(ctx);
        }

        [UnityTest]
        public IEnumerator TriggerVolume_TriggerBeforeSetup_PushesBufferedLevelOnSetup()
        {
            var volume = CreateVolume("in-zone");
            CreatePlayer(Vector3.zero);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            Assert.IsFalse(bus.Get("in-zone"));

            yield return volume.Setup(ctx);
            Assert.IsTrue(bus.Get("in-zone"),
                "Occupancy accumulated before Setup must be pushed onto the bus when Setup wires it.");

            yield return volume.Teardown(ctx);
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
        public IEnumerator BlankToken_Volume_LogsError_AndStaysInert()
        {
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var volume = CreateVolume("");

            LogAssert.Expect(LogType.Error, new Regex("TriggerVolume .*blank signal token.*inert"));
            yield return volume.Setup(ctx);

            var changes = 0;
            bus.Changed += _ => changes++;

            CreatePlayer(Vector3.zero);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, changes, "An inert volume must write nothing to the bus.");
        }

        [UnityTest]
        public IEnumerator ParkedThenQualified_RuleFires_WhenLatchedTermArrivesWhileParkedInside()
        {
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);

            var volume = CreateVolume("in-gate");
            var rule = TrackGO(new GameObject("ExtractionRule")).AddComponent<ActivationRule>();
            rule.Configure(
                new[] { ActivationTerm.Signal("in-gate"), ActivationTerm.Signal("key-acquired") },
                new[] { "challenge-started" });

            yield return volume.Setup(ctx);
            yield return rule.Setup(ctx);

            var fired = 0;
            rule.Fired += () => fired++;

            var player = CreatePlayer(Vector3.zero);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(bus.Get("in-gate"));
            Assert.AreEqual(0, fired, "Parked in the gate without the key must not fire the rule.");

            bus.Latch("key-acquired");
            Assert.AreEqual(1, fired,
                "The rule must fire for a player already parked inside when the latched term arrives — no enter-edge needed.");
            Assert.IsTrue(bus.Get("challenge-started"), "Firing must publish the rule's latched tokens.");

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
