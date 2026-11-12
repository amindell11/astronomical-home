#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Weapons
{
    [Category("Weapons")]
    public class WeaponRecoveryPlayModeTests : PlayModeWorldFixture
    {
        private readonly List<GameObject> spawned = new();
        private int recoveries;

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        // Disabled, so only the test's own ticks advance it.
        private T Condition<T>() where T : WeaponCondition
        {
            var go = new GameObject(typeof(T).Name);
            spawned.Add(go);
            var condition = go.AddComponent<T>();
            condition.enabled = false;
            ((IRecoveryReadout)condition).Recovered += () => recoveries++;
            return condition;
        }

        [Test]
        public void Heat_RecoversOnce_WhenTheOverheatLockoutEnds()
        {
            var heat = Condition<Heat>();
            heat.Configure(100f, 25f, 20f, 0f, 0f);
            heat.ProcessFire();
            heat.Tick(5f);
            Assert.That(heat.CurrentHeat, Is.Zero);
            Assert.That(recoveries, Is.Zero, "cooling without a lockout is no recovery");

            heat.Configure(100f, 100f, 20f, 0f, 0f);
            heat.ProcessFire();
            for (var step = 0; step < 4; step++)
                heat.Tick(1f);
            Assert.That(heat.Overheated, Is.True);
            Assert.That(recoveries, Is.Zero, "partial cooling keeps the lockout");
            heat.Tick(1f);
            Assert.That(heat.Overheated, Is.False);
            Assert.That(recoveries, Is.EqualTo(1));
            for (var step = 0; step < 3; step++)
                heat.Tick(1f);
            Assert.That(recoveries, Is.EqualTo(1));
        }

        [Test]
        public void SequentialRounds_RecoverOncePerRegainedRound()
        {
            var rounds = Condition<Rounds>();
            rounds.Configure(3, 1f, Rounds.RefillMode.Sequential);
            for (var shot = 0; shot < 3; shot++)
                rounds.ProcessFire();
            rounds.Tick(.5f);
            Assert.That(recoveries, Is.Zero);
            for (var loaded = 1; loaded <= 3; loaded++)
            {
                rounds.Tick(.5f);
                rounds.Tick(.5f);
                Assert.That(rounds.AmmoCount, Is.EqualTo(loaded));
                Assert.That(recoveries, Is.EqualTo(loaded));
            }
            rounds.Tick(5f);
            Assert.That(recoveries, Is.EqualTo(3), "a full launcher regains nothing");
        }

        [Test]
        public void Magazine_RecoversOnce_WhenTheReloadLands()
        {
            var rounds = Condition<Rounds>();
            rounds.Configure(4, 1f);
            while (rounds.AmmoCount > 0) rounds.ProcessFire();
            rounds.Tick(.4f);
            rounds.Tick(.4f);
            Assert.That(recoveries, Is.Zero);
            rounds.Tick(.4f);
            Assert.That(rounds.AmmoCount, Is.EqualTo(4));
            Assert.That(recoveries, Is.EqualTo(1));
            rounds.Tick(1f);
            rounds.Reset();
            Assert.That(recoveries, Is.EqualTo(1), "a reset is no recovery");
        }

        [UnityTest]
        public IEnumerator Railgun_RecoversOnce_WhenItsCooldownEnds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Railguns>("Assets/Prefabs/Weapons/Railgun.prefab");
            var railgun = Object.Instantiate(prefab);
            spawned.Add(railgun.gameObject);
            var cooldown = railgun.GetComponent<Cooldown>();
            using (var serialized = new SerializedObject(cooldown))
            {
                serialized.FindProperty("fireRate").floatValue = .1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var recovery = railgun.Readouts.OfType<IRecoveryReadout>().Single();
            Assert.AreSame(railgun, recovery);
            recovery.Recovered += () => recoveries++;

            cooldown.ProcessFire();
            var until = Time.realtimeSinceStartup + .4f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                Assert.That(recoveries, Is.LessThanOrEqualTo(1));
            }
            Assert.That(recoveries, Is.EqualTo(1));
            yield return null;
            yield return null;
            Assert.That(recoveries, Is.EqualTo(1));
        }
    }
}
#endif
