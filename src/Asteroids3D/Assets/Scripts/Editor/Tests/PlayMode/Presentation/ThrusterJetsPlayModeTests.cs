using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ships.Movement;
using Ships.Presentation;
using Ships.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    /// <summary>The jets follow the applied thrust, each case on a fresh Bulwark rig.</summary>
    [Category("Ships")]
    public sealed class ThrusterJetsPlayModeTests
    {
        private const string BulwarkPath = "Assets/Prefabs/Ships/Bulwark.prefab";

        private GameObject rig;
        private AppliedThrust applied;

        [SetUp]
        public void SetUp()
        {
            applied = default;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulwarkPath);
            rig = Object.Instantiate(prefab.GetComponentInChildren<ShipVisualRig>(true).gameObject);
            var thrusters = rig.GetComponentInChildren<ThrusterVisuals>(true);
            thrusters.Bind(new ShipView(rig.transform, null, null, null, false, thrust: () => applied));
        }

        [TearDown]
        public void TearDown()
        {
            if (rig) Object.Destroy(rig);
        }

        [UnityTest]
        public IEnumerator AtRest_NoJetFires()
        {
            applied = default;
            yield return null;
            yield return null;
            CollectionAssert.AreEquivalent(new string[] { }, PlayingSockets());
        }

        [UnityTest]
        public IEnumerator ThrustRight_FiresBothLeftSideJets()
        {
            applied = new AppliedThrust(new Vector2(1f, 0f), 0f, 0f);
            yield return null;
            yield return null;
            CollectionAssert.AreEquivalent(new[] { "BowLeft", "SternLeft" }, PlayingSockets());
        }

        [UnityTest]
        public IEnumerator ThrustBack_FiresBothProwJets()
        {
            applied = new AppliedThrust(new Vector2(0f, -1f), 0f, 0f);
            yield return null;
            yield return null;
            CollectionAssert.AreEquivalent(new[] { "BowForwardLeft", "BowForwardRight" }, PlayingSockets());
        }

        [UnityTest]
        public IEnumerator PositiveYaw_FiresTheBowRightSternLeftCouple()
        {
            applied = new AppliedThrust(Vector2.zero, 1f, 0f);
            yield return null;
            yield return null;
            var lit = PlayingSockets();
            Assert.That(lit, Has.Member("BowRight"));
            Assert.That(lit, Has.Member("SternLeft"));
            Assert.That(lit, Has.No.Member("BowLeft"));
            Assert.That(lit, Has.No.Member("SternRight"));
        }

        [UnityTest]
        public IEnumerator PositiveRoll_FiresTheLeftTopRightBottomCouple()
        {
            applied = new AppliedThrust(Vector2.zero, 0f, 1f);
            yield return null;
            yield return null;
            var lit = PlayingSockets();
            Assert.That(lit, Has.Member("RollLeftTop"));
            Assert.That(lit, Has.Member("RollRightBottom"));
            Assert.That(lit, Has.No.Member("RollLeftBottom"));
            Assert.That(lit, Has.No.Member("RollRightTop"));
        }

        [UnityTest]
        public IEnumerator WeakerThrust_ShowsASmallerJet()
        {
            Transform bowLeft = null;
            foreach (var system in rig.GetComponentsInChildren<ParticleSystem>(true))
                if (system.name == "RcsJet" && system.transform.parent.name == "BowLeft")
                    bowLeft = system.transform;
            Assert.That(bowLeft != null, Is.True, "BowLeft has no RcsJet");

            applied = new AppliedThrust(new Vector2(0.3f, 0f), 0f, 0f);
            yield return null;
            yield return null;
            var weak = bowLeft.localScale.magnitude;
            applied = new AppliedThrust(new Vector2(1f, 0f), 0f, 0f);
            yield return null;
            yield return null;
            Assert.That(bowLeft.localScale.magnitude, Is.GreaterThan(weak));
        }

        private string[] PlayingSockets()
        {
            var names = new List<string>();
            foreach (var system in rig.GetComponentsInChildren<ParticleSystem>(true))
                if (system.name == "RcsJet" && system.isPlaying)
                    names.Add(system.transform.parent.name);
            return names.ToArray();
        }
    }
}
