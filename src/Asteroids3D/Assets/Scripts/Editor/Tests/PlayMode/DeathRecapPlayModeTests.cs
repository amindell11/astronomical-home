#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Damage;
using Game.Player;
using NUnit.Framework;
using Ships.Registry;
using UI.Screens;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode
{
    /// <summary>
    /// The death recap renders the run tally's kills and time survived as their own lines, and the
    /// burst chart above the whole-life table.
    /// </summary>
    [TestFixture]
    [Category("UI")]
    public class DeathRecapPlayModeTests
    {
        private sealed class FixedTally : IRunTally
        {
            public int Kills => 7;
            public float SecondsSurvived => 83.4f;
        }

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.Destroy(_root);
        }

        [UnityTest]
        public IEnumerator Recap_RendersKillsAndTimeSurvived()
        {
            _root = new GameObject("RecapRoot");
            var screen = DeathRecapScreen.Create(_root.transform);
            var blow = new DamageInfo(10f, DamageKind.Collision, ShipId.Invalid, 0f, Vector3.zero, Vector3.zero);

            screen.Show(blow, new DamageLedger(), new FixedTally(), null);
            yield return null;

            var lines = screen.GetComponentsInChildren<Text>().SelectMany(t => t.text.Split('\n')).ToList();
            CollectionAssert.Contains(lines, "Kills: 7");
            CollectionAssert.Contains(lines, "Time survived: 1:23");
        }

        [UnityTest]
        public IEnumerator Recap_DrawsTheBurstChartAboveTheTable_WithTheKillingBlowMarked()
        {
            _root = new GameObject("RecapRoot");
            var screen = DeathRecapScreen.Create(_root.transform);
            var ledger = new DamageLedger();
            var blow = new DamageInfo(10f, DamageKind.Laser, new ShipId(7), 0f, Vector3.zero, Vector3.zero);
            ledger.Record(new DamageInfo(30f, DamageKind.Missile, new ShipId(9), 0f, Vector3.zero, Vector3.zero), 1f);
            ledger.Record(blow, 2f);

            screen.Show(blow, ledger, new FixedTally(), null);
            yield return null;

            var panel = screen.transform.Find("Dim/Panel");
            var chart = panel.Find("BurstChart");
            Assert.IsNotNull(chart, "a life with hits draws the burst chart");
            Assert.Less(chart.GetSiblingIndex(), panel.Find("Rows").GetSiblingIndex(),
                "the chart sits above the whole-life table");
            var lastBucket = chart.Find($"Bucket{DamageBurst.BucketCount - 1}");
            Assert.AreEqual(3, lastBucket.childCount, "both sources stack in the last bucket, plus the mark");
            Assert.IsNotNull(lastBucket.Find("KillingBlow"), "the killing blow is marked on the last bucket");
        }
    }
}
#endif
