using Damage;
using Game.Player;
using NUnit.Framework;
using Ships.Damage;
using Ships.Loadout;
using Ships.Registry;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Run tally kill attribution: a killing blow from the player counts and raises Killed; asteroid, other-ship and self kills do not, and nothing counts outside Begin/End.</summary>
    [Category("Damage")]
    public class RunTallyEditModeTests
    {
        private static readonly ShipId Player = new(1);

        private GameObject _go;
        private RunTally _tally;
        private DamageController _victim;
        private ShipId _playerId;
        private int _killedRaised;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TallyVictim");
            _victim = _go.AddComponent<DamageController>();
            _victim.PopulateSettings(new ResolvedShipStats { maxHealth = 10f, maxShield = 0f });

            _playerId = Player;
            _killedRaised = 0;
            _tally = new RunTally();
            _tally.Bind(null, () => _playerId);
            _tally.Killed += () => _killedRaised++;
            _tally.Begin(0f);
            _tally.Watch(_victim);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
        }

        private void Kill(ShipId attacker, DamageKind kind = DamageKind.Laser) =>
            _victim.TakeDamage(new DamageInfo(100f, kind, attacker, 0f, Vector3.zero, Vector3.zero));

        // Id 0 is ShipId.Invalid: an asteroid's attacker id, and this Ship-less victim's own id.
        [TestCase(1, 1, DamageKind.Laser, 1, TestName = "PlayerKillingBlow_CountsAndRaisesKilled")]
        [TestCase(1, 0, DamageKind.Collision, 0, TestName = "AsteroidKill_DoesNotCountOrRaiseKilled")]
        [TestCase(1, 2, DamageKind.Laser, 0, TestName = "OtherAttackerKill_DoesNotCountOrRaiseKilled")]
        [TestCase(0, 0, DamageKind.Laser, 0, TestName = "PlayerSelfDeath_DoesNotCountOrRaiseKilled")]
        public void KillingBlow_CountsAndRaisesKilled_OnlyWhenThePlayerKillsAnotherShip(
            int playerId, int attackerId, DamageKind kind, int expectedKills)
        {
            _playerId = new ShipId(playerId);
            Kill(new ShipId(attackerId), kind);
            Assert.AreEqual(expectedKills, _tally.Kills);
            Assert.AreEqual(expectedKills, _killedRaised);
        }

        [Test]
        public void KillAfterEnd_DoesNotCountOrRaiseKilled_AndTimeFreezes()
        {
            _tally.End(42f);
            Kill(Player);
            Assert.AreEqual(0, _tally.Kills, "The run is over once the host stamps its end.");
            Assert.AreEqual(0, _killedRaised);
            Assert.AreEqual(42f, _tally.SecondsSurvived, 1e-4f);
        }

        [Test]
        public void KillBeforeBegin_DoesNotRaiseKilled()
        {
            _tally.Reset();
            Kill(Player);
            Assert.AreEqual(0, _killedRaised);
        }

        [Test]
        public void FormatSeconds_ReadsMinutesAndSeconds()
        {
            Assert.AreEqual("0:07", RunTally.FormatSeconds(7.9f));
            Assert.AreEqual("2:05", RunTally.FormatSeconds(125f));
        }
    }
}
