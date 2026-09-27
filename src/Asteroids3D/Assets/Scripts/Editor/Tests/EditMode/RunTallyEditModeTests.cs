using Damage;
using Game.Player;
using NUnit.Framework;
using Ships.Damage;
using Ships.Loadout;
using Ships.Registry;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Run tally kill attribution: a killing blow from the player counts, asteroid and other-ship kills do not, and nothing counts outside Begin/End.</summary>
    [Category("Damage")]
    public class RunTallyEditModeTests
    {
        private static readonly ShipId Player = new(1);
        private static readonly ShipId Other = new(2);

        private GameObject _go;
        private RunTally _tally;
        private DamageController _victim;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TallyVictim");
            _victim = _go.AddComponent<DamageController>();
            _victim.PopulateSettings(new ResolvedShipStats { maxHealth = 10f, maxShield = 0f });

            _tally = new RunTally();
            _tally.Bind(null, () => Player);
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

        [Test]
        public void PlayerKillingBlow_Counts()
        {
            Kill(Player);
            Assert.AreEqual(1, _tally.Kills);
        }

        [Test]
        public void AsteroidKill_DoesNotCount()
        {
            Kill(ShipId.Invalid, DamageKind.Collision);
            Assert.AreEqual(0, _tally.Kills);
        }

        [Test]
        public void OtherAttackerKill_DoesNotCount()
        {
            Kill(Other);
            Assert.AreEqual(0, _tally.Kills);
        }

        [Test]
        public void KillAfterEnd_DoesNotCount_AndTimeFreezes()
        {
            _tally.End(42f);
            Kill(Player);
            Assert.AreEqual(0, _tally.Kills, "The run is over once the host stamps its end.");
            Assert.AreEqual(42f, _tally.SecondsSurvived, 1e-4f);
        }

        [Test]
        public void FormatSeconds_ReadsMinutesAndSeconds()
        {
            Assert.AreEqual("0:07", RunTally.FormatSeconds(7.9f));
            Assert.AreEqual("2:05", RunTally.FormatSeconds(125f));
        }
    }
}
