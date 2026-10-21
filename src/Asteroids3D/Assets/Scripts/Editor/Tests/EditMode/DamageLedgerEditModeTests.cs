using Damage;
using NUnit.Framework;
using Game.Player;
using Ships.Damage;
using Ships.Loadout;
using UI;
using UnityEngine;
using UI.Screens;
using Ships.Registry;

namespace Tests.EditMode
{
    /// <summary>
    /// Ledger aggregation and recap copy run headless off a real DamageController (PopulateSettings
    /// init path, no Awake); burst-chart cases record straight into the ledger at chosen times.
    /// Ship-name resolution needs a live registry and stays in-editor.
    /// </summary>
    [Category("Damage")]
    public class DamageLedgerEditModeTests
    {
        private GameObject _go;

        private DamageController NewDamage()
        {
            _go = new GameObject("LedgerTest");
            var dc = _go.AddComponent<DamageController>();
            dc.PopulateSettings(new ResolvedShipStats
            {
                maxHealth = 100f,
                maxShield = 50f,
                shieldRegenRate = 10f,
                shieldRegenDelay = 0.5f,
            });
            return dc;
        }

        private DamageLedger NewBoundLedger(out DamageController dc)
        {
            dc = NewDamage();
            var ledger = new DamageLedger();
            ledger.Bind(dc, null);
            return ledger;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            _go = null;
        }

        private static DamageInfo Hit(float amount, DamageKind kind, int attacker) =>
            new(amount, kind, new ShipId(attacker), 0f, Vector3.zero, Vector3.zero);

        [Test]
        public void Rows_AggregatePerAttackerAndKind()
        {
            var ledger = NewBoundLedger(out var dc);

            dc.TakeDamage(Hit(10f, DamageKind.Laser, 7));
            dc.TakeDamage(Hit(15f, DamageKind.Laser, 7));
            dc.TakeDamage(Hit(5f, DamageKind.Missile, 7));
            dc.TakeDamage(Hit(8f, DamageKind.Laser, 9));

            Assert.AreEqual(3, ledger.Rows.Count, "one row per (attacker, kind) pair");
            var laser7 = ledger.Rows[0];
            Assert.AreEqual(25f, laser7.Total, 0.001f, "same-source hits accumulate");
            Assert.AreEqual(2, laser7.Hits);
        }

        [Test]
        public void Rows_RecordAppliedDamage_NotIncoming()
        {
            var ledger = NewBoundLedger(out var dc);

            dc.TakeDamage(Hit(500f, DamageKind.Railgun, 7)); // 50 shield + 100 hull absorbed

            Assert.AreEqual(150f, ledger.Rows[0].Total, 0.001f,
                "row totals use the event's applied amount, capped at shield + hull");
        }

        [Test]
        public void NameFallback_CollisionIsAsteroid_UnresolvedShipIsUnknown()
        {
            var ledger = NewBoundLedger(out var dc);

            dc.TakeDamage(new DamageInfo(5f, DamageKind.Collision, ShipId.Invalid,
                0f, Vector3.zero, Vector3.zero));
            dc.TakeDamage(Hit(5f, DamageKind.Laser, 42));

            Assert.AreEqual("asteroid", ledger.Rows[0].SourceName);
            Assert.AreEqual("unknown", ledger.Rows[1].SourceName);
        }

        [Test]
        public void Hits_KeepEveryHitWithItsTime_BesideThePerSourceTotals()
        {
            var ledger = new DamageLedger();

            ledger.Record(Hit(10f, DamageKind.Laser, 7), 3f);
            ledger.Record(Hit(15f, DamageKind.Laser, 7), 4.5f);

            Assert.AreEqual(2, ledger.Hits.Count, "one record per hit");
            var second = ledger.Hits[1];
            Assert.AreEqual(4.5f, second.Time);
            Assert.AreEqual(15f, second.Amount);
            Assert.AreEqual(DamageKind.Laser, second.Kind);
            Assert.AreEqual(new ShipId(7), second.AttackerId);
            Assert.AreEqual(1, ledger.Rows.Count, "the hits still aggregate into one row");
            Assert.AreEqual(25f, ledger.Rows[0].Total, 0.001f);
        }

        [Test]
        public void Clear_EmptiesRowsAndHits_BindingSurvives()
        {
            var ledger = NewBoundLedger(out var dc);

            dc.TakeDamage(Hit(10f, DamageKind.Laser, 7));
            ledger.Clear();
            Assert.AreEqual(0, ledger.Rows.Count);
            Assert.AreEqual(0, ledger.Hits.Count, "a new life starts with no hits");

            dc.TakeDamage(Hit(10f, DamageKind.Laser, 7));
            Assert.AreEqual(1, ledger.Rows.Count, "the ledger keeps recording after Clear");
        }

        [Test]
        public void Rebind_StopsRecordingFromTheOldSource()
        {
            var ledger = NewBoundLedger(out var dc);
            ledger.Bind(null, null);

            dc.TakeDamage(Hit(10f, DamageKind.Laser, 7));
            Assert.AreEqual(0, ledger.Rows.Count, "an unbound ledger records nothing");
        }

        [Test]
        public void CauseLine_Collision_ReadsAsFlyingIntoAnAsteroid()
        {
            var blow = new DamageInfo(30f, DamageKind.Collision, ShipId.Invalid,
                0f, Vector3.zero, Vector3.zero);
            Assert.AreEqual("You flew into an asteroid.", DeathRecapScreen.CauseLine(blow, null));
        }

        [Test]
        public void CauseLine_ShipKill_NamesTheKillerFromItsRow()
        {
            var ledger = NewBoundLedger(out var dc);
            dc.TakeDamage(Hit(500f, DamageKind.Missile, 7));

            var blow = Hit(500f, DamageKind.Missile, 7);
            StringAssert.Contains("unknown", DeathRecapScreen.CauseLine(blow, ledger.Rows));
            StringAssert.Contains("missile", DeathRecapScreen.CauseLine(blow, ledger.Rows));
        }

        [Test]
        public void RowsBlock_SortsByTotal_AndCountsHits()
        {
            var ledger = NewBoundLedger(out var dc);
            dc.TakeDamage(Hit(5f, DamageKind.Laser, 7));
            dc.TakeDamage(Hit(30f, DamageKind.Missile, 9));

            var block = DeathRecapScreen.RowsBlock(ledger.Rows);
            StringAssert.Contains("missile: 30 dmg (1 hit)", block);
            Assert.Less(block.IndexOf("missile"), block.IndexOf("laser fire"),
                "heaviest source lists first");
        }

        [Test]
        public void Burst_BucketsTheLastTenSeconds_EndingAtTheKillingBlow()
        {
            var ledger = new DamageLedger();
            ledger.Record(Hit(5f, DamageKind.Laser, 7), 100f);
            ledger.Record(Hit(10f, DamageKind.Laser, 7), 110.5f);
            ledger.Record(Hit(20f, DamageKind.Laser, 7), 115.2f);
            ledger.Record(Hit(7f, DamageKind.Laser, 7), 120f);

            var burst = DamageBurst.From(ledger, 5);

            Assert.AreEqual(10f, burst.Amount(0, 0), 0.001f,
                "a hit 9.5 s before the killing blow opens the window");
            Assert.AreEqual(20f, burst.Amount(5, 0), 0.001f);
            Assert.AreEqual(7f, burst.Amount(DamageBurst.BucketCount - 1, 0), 0.001f,
                "the killing blow lands in the last bucket");
            var charted = 0f;
            for (var b = 0; b < DamageBurst.BucketCount; b++) charted += burst.Amount(b, 0);
            Assert.AreEqual(37f, charted, 0.001f, "the hit 20 s before death falls outside the window");
        }

        [Test]
        public void Burst_StacksSourcesHeaviestFirst_AndMarksTheKillingBlowsSource()
        {
            var ledger = new DamageLedger();
            ledger.Record(Hit(10f, DamageKind.Laser, 7), 9.2f);
            ledger.Record(Hit(20f, DamageKind.Missile, 9), 9.5f);
            ledger.Record(Hit(5f, DamageKind.Laser, 7), 10f);

            var burst = DamageBurst.From(ledger, 5);

            Assert.AreEqual(2, burst.SeriesCount);
            Assert.AreEqual(DamageKind.Missile, burst.Sources[0].Kind,
                "the heaviest source in the window stacks lowest");
            var last = DamageBurst.BucketCount - 1;
            Assert.AreEqual(20f, burst.Amount(last, 0), 0.001f);
            Assert.AreEqual(15f, burst.Amount(last, 1), 0.001f);
            Assert.AreEqual(35f, burst.Peak, 0.001f, "the peak is the tallest bucket's stacked total");
            Assert.AreEqual(1, burst.KillingSeries, "the killing blow's source, not the heaviest");
            StringAssert.Contains("laser fire (killing blow)", DeathRecapScreen.LegendBlock(burst));
        }

        [Test]
        public void Burst_FoldsSourcesPastTheNamedCapIntoOther()
        {
            var ledger = new DamageLedger();
            ledger.Record(Hit(30f, DamageKind.Laser, 7), 1f);
            ledger.Record(Hit(20f, DamageKind.Laser, 8), 2f);
            ledger.Record(Hit(10f, DamageKind.Laser, 9), 3f);
            ledger.Record(Hit(5f, DamageKind.Missile, 9), 4f);

            var burst = DamageBurst.From(ledger, 2);

            Assert.AreEqual(2, burst.Sources.Count);
            Assert.IsTrue(burst.HasOther);
            Assert.AreEqual(3, burst.SeriesCount);
            var other = 0f;
            for (var b = 0; b < DamageBurst.BucketCount; b++) other += burst.Amount(b, 2);
            Assert.AreEqual(15f, other, 0.001f, "the two lightest sources stack as one other series");
            Assert.AreEqual(2, burst.KillingSeries, "a folded killing blow marks the other series");
        }
    }
}
