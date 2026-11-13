using Damage;
using Game.Player;
using NUnit.Framework;
using Ships.Damage;
using Ships.Loadout;
using Ships.Registry;
using UnityEngine;

namespace Tests.EditMode.Runs
{
    /// <summary>Damage the player dealt: only the player's hits on other ships count, only between Begin and End, split by sector and kind.</summary>
    [Category("Bootstrap")]
    public class DamageDealtEditModeTests
    {
        private static readonly ShipId Player = new(1);
        private static readonly ShipId Victim = new(2);

        private GameObject go;
        private DamageDealt dealt;
        private DamageController damage;
        private ShipId playerId;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("DealtVictim");
            damage = go.AddComponent<DamageController>();
            damage.PopulateSettings(new ResolvedShipStats { maxHealth = 100f, maxShield = 50f });
            playerId = Player;
            dealt = new DamageDealt();
            dealt.Bind(null, () => playerId);
        }

        [TearDown]
        public void TearDown()
        {
            if (go) Object.DestroyImmediate(go);
        }

        private void Arm(ShipId victim, int sector)
        {
            dealt.Begin();
            dealt.BeginSector(sector);
            dealt.Watch(damage, victim);
        }

        private void Hit(ShipId attacker, float amount, DamageKind kind) =>
            damage.TakeDamage(new DamageInfo(amount, kind, attacker, 0f, Vector3.zero, Vector3.zero));

        [Test]
        public void PlayerHits_AggregateShieldHullAndHits_AndIgnoreOtherAttackers()
        {
            Arm(Victim, 1);
            Hit(Player, 60f, DamageKind.Laser);
            Hit(new ShipId(3), 40f, DamageKind.Laser);

            Assert.AreEqual(1, dealt.Rows.Count);
            Assert.AreEqual(1, dealt.Rows[0].Sector);
            Assert.AreEqual(DamageKind.Laser, dealt.Rows[0].Kind);
            Assert.AreEqual(50f, dealt.Rows[0].Shield, 1e-4f);
            Assert.AreEqual(10f, dealt.Rows[0].Hull, 1e-4f);
            Assert.AreEqual(60f, dealt.Rows[0].Total, 1e-4f);
            Assert.AreEqual(1, dealt.Rows[0].Hits);
        }

        [Test]
        public void AHitOnThePlayer_DoesNotCount()
        {
            Arm(Player, 1);
            Hit(Player, 60f, DamageKind.Laser);

            Assert.IsEmpty(dealt.Rows);
        }

        [Test]
        public void HitsOutsideBeginAndEnd_DoNotCount()
        {
            dealt.Watch(damage, Victim);
            Hit(Player, 10f, DamageKind.Laser);
            dealt.Begin();
            dealt.BeginSector(1);
            Hit(Player, 10f, DamageKind.Laser);
            dealt.End();
            Hit(Player, 10f, DamageKind.Laser);

            Assert.AreEqual(1, dealt.Rows.Count);
            Assert.AreEqual(1, dealt.Rows[0].Hits);
        }

        [Test]
        public void Hits_SplitBySectorAndKind()
        {
            Arm(Victim, 1);
            Hit(Player, 10f, DamageKind.Laser);
            dealt.BeginSector(2);
            Hit(Player, 10f, DamageKind.Laser);
            Hit(Player, 10f, DamageKind.Missile);

            Assert.AreEqual(3, dealt.Rows.Count);
            Assert.AreEqual(1, dealt.Rows[0].Sector);
            Assert.AreEqual(DamageKind.Laser, dealt.Rows[0].Kind);
            Assert.AreEqual(2, dealt.Rows[1].Sector);
            Assert.AreEqual(DamageKind.Laser, dealt.Rows[1].Kind);
            Assert.AreEqual(2, dealt.Rows[2].Sector);
            Assert.AreEqual(DamageKind.Missile, dealt.Rows[2].Kind);
        }
    }
}
