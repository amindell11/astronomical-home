#if UNITY_EDITOR
using System.Collections;
using Game.Player;
using NUnit.Framework;
using Ships;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Bootstrap
{
    /// <summary>A ship the unit service revives in place opens a fresh spawn-log entry per life.</summary>
    [Category("Bootstrap")]
    public class SpawnLogRespawnPlayModeTests : PlayModeWorldFixture
    {
        private GameObject host;

        public override void TearDown()
        {
            if (host) host.GetComponent<UnitService>().Clear();
            DestroyTestObject(host);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator DieAndRevive_LogsOneEntryPerLife()
        {
            host = new GameObject("TestUnitService");
            var units = host.AddComponent<UnitService>();
            units.Initialize(Projectiles, false, host.transform);
            var template = TestAssets.LoadShip2Prefab();

            var log = new SpawnLog();
            Ship player = null;
            log.Bind(units, () => player.Id);
            player = units.SpawnShip(template, null, 0, new Vector3(-50f, 0f, 0f), Quaternion.identity, null);
            var enemy = units.SpawnShip(template, null, 1, new Vector3(50f, 0f, 0f), Quaternion.identity, null);
            log.Begin(Time.time);

            yield return null;
            TestDamage.Kill(enemy, player);
            Assert.IsFalse(enemy.gameObject.activeSelf, "the first life ended");

            yield return null;
            units.RespawnShip(enemy.Id, new Vector2(50f, 0f), 0f);
            Assert.IsTrue(enemy.gameObject.activeSelf, "the ship was revived");

            yield return null;
            log.End(Time.time);

            var entries = log.Entries();
            Assert.AreEqual(2, entries.Count, "one entry per life");
            Assert.AreEqual(enemy.Id, entries[0].Id);
            Assert.AreEqual(enemy.Id, entries[1].Id);
            Assert.IsTrue(entries[0].KilledByPlayer, "the first life ended on the player's shot");
            Assert.IsFalse(entries[1].KilledByPlayer, "the second life survives to the run's end");
            Assert.Greater(entries[1].SpawnSeconds, entries[0].SpawnSeconds, "the revive has its own spawn time");
            Assert.Less(entries[0].AliveSeconds, entries[1].SpawnSeconds - entries[0].SpawnSeconds,
                "the first life's alive clock stopped at its death, a frame before the revive");
        }
    }
}
#endif
