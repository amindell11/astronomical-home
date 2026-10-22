using Damage;
using NUnit.Framework;
using Game.Player;
using Ships.Damage;
using Ships.Loadout;
using UnityEngine;
using Ships.Registry;

namespace Tests.EditMode
{
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

        [TestCase(25f, 25f, 0f)]
        [TestCase(75f, 50f, 25f)]
        [TestCase(500f, 50f, 100f)]
        public void AppliedSplit_RecordsShieldHullAndCappedLethalDamage(float incoming, float shield, float hull)
        {
            var ledger = NewBoundLedger(out var dc);
            dc.TakeDamage(Hit(incoming, DamageKind.Missile, 7));

            Assert.AreEqual(shield, ledger.Rows[0].Shield);
            Assert.AreEqual(hull, ledger.Rows[0].Hull);
            Assert.AreEqual(shield, ledger.Hits[0].Shield);
            Assert.AreEqual(hull, ledger.Hits[0].Hull);
        }

        [Test]
        public void RecoveryBetweenHits_DoesNotEraseDamageAlreadyTaken()
        {
            var ledger = NewBoundLedger(out var dc);
            dc.TakeDamage(Hit(75f, DamageKind.Laser, 7));
            dc.Health.RestoreFraction(0.1f);
            dc.Shield.Update(10f);
            dc.TakeDamage(Hit(75f, DamageKind.Laser, 7));

            Assert.AreEqual(100f, ledger.Rows[0].Shield);
            Assert.AreEqual(50f, ledger.Rows[0].Hull);
            Assert.AreEqual(60f, dc.Health.CurrentValue);
        }

        [Test]
        public void SectorResourceReset_PreservesTheWholeRunLedger()
        {
            var ledger = NewBoundLedger(out var dc);
            dc.TakeDamage(Hit(75f, DamageKind.Laser, 7));
            dc.ResetDamageState();
            dc.TakeDamage(Hit(90f, DamageKind.Laser, 7));

            Assert.AreEqual(100f, ledger.Rows[0].Shield);
            Assert.AreEqual(65f, ledger.Rows[0].Hull);
            Assert.AreEqual(2, ledger.Hits.Count);
        }

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

            ledger.Record(new AppliedDamage(Hit(10f, DamageKind.Laser, 7), 7f, 3f), 3f);
            ledger.Record(new AppliedDamage(Hit(15f, DamageKind.Laser, 7), 4f, 11f), 4.5f);

            Assert.AreEqual(2, ledger.Hits.Count, "one record per hit");
            var second = ledger.Hits[1];
            Assert.AreEqual(4.5f, second.Time);
            Assert.AreEqual(15f, second.Amount);
            Assert.AreEqual(4f, second.Shield);
            Assert.AreEqual(11f, second.Hull);
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

    }
}
