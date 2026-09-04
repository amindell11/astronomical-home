using Damage;
using NUnit.Framework;
using Player;
using Ships;
using Ships.Damage;
using UI;
using UnityEngine;
using Ships.Registry;

namespace Tests.EditMode
{
    /// <summary>
    /// Ledger aggregation and recap copy run headless off a real DamageController (PopulateSettings
    /// init path, no Awake); ship-name resolution needs a live registry and stays in-editor.
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
        public void Clear_EmptiesRows_BindingSurvives()
        {
            var ledger = NewBoundLedger(out var dc);

            dc.TakeDamage(Hit(10f, DamageKind.Laser, 7));
            ledger.Clear();
            Assert.AreEqual(0, ledger.Rows.Count);

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
    }
}
