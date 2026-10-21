#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Projectiles;
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
    /// <summary>
    /// Timed weapon conditions fire on the fixed step their authored values imply: a made-up weapon is
    /// triggered once per step, and the steps it launches on are checked to within half a step.
    /// </summary>
    [Category("Weapons")]
    public class WeaponConditionTimingPlayModeTests : PlayModeWorldFixture
    {
        private readonly List<GameObject> spawned = new();
        private float savedTimeScale;
        private float savedCaptureDelta;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            savedTimeScale = Time.timeScale;
            savedCaptureDelta = Time.captureDeltaTime;
            // One Update per fixed step, so Update-ticked conditions advance in step with the trigger.
            Time.timeScale = 1f;
            Time.captureDeltaTime = Time.fixedDeltaTime;
        }

        [TearDown]
        public override void TearDown()
        {
            Time.timeScale = savedTimeScale;
            Time.captureDeltaTime = savedCaptureDelta;
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        // Tighter than one step, so a timed event landing a step off fails.
        private static float HalfStep => Time.fixedDeltaTime * 0.5f;

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Inactive, so nothing on it wakes until its configured clone does.
        private T Template<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            go.SetActive(false);
            spawned.Add(go);
            return go.AddComponent<T>();
        }

        private Laser Bolt()
        {
            var bolt = Template<Laser>();
            bolt.gameObject.AddComponent<Rigidbody>().useGravity = false;
            return bolt;
        }

        private static void AddCooldown(Component weapon, float secondsBetweenShots) =>
            SetFloat(weapon.gameObject.AddComponent<Cooldown>(), "fireRate", secondsBetweenShots);

        private IEnumerator Fire(WeaponComponent template, Func<WeaponComponent, bool> held, float seconds,
            List<float> launches)
        {
            var weapon = Object.Instantiate(template);
            spawned.Add(weapon.gameObject);
            weapon.gameObject.SetActive(true);

            var start = Time.fixedTime;
            while (Time.fixedTime - start < seconds)
            {
                yield return new WaitForFixedUpdate();
                var before = Projectiles.ActiveCount;
                var down = held(weapon);
                weapon.HandleTrigger(new WeaponCommand { pressed = down, held = down, targetPoint = weapon.firePoint.position + weapon.firePoint.up * 10f }, Projectiles);
                if (Projectiles.ActiveCount > before)
                    launches.Add(Time.fixedTime - start);
            }
            TestContext.WriteLine($"{template.name} launched at: {string.Join(", ", launches)}");
        }

        [UnityTest]
        public IEnumerator Magazine_DumpsAndReloadsOnTheAuthoredSteps()
        {
            var rippers = Template<Rippers>();
            rippers.projectilePrefab = Bolt();
            AddCooldown(rippers, secondsBetweenShots: 0.2f);
            rippers.gameObject.AddComponent<Rounds>().Configure(maxAmmo: 4, reloadTime: 1f);

            var launches = new List<float>();
            yield return Fire(rippers, _ => true, seconds: 2f, launches);

            Assert.That(launches.Count, Is.GreaterThanOrEqualTo(5), "one magazine and the first round after reload");
            Assert.AreEqual(0.6f, launches[3] - launches[0], HalfStep, "four rounds 0.2 s apart");
            Assert.AreEqual(1f, launches[4] - launches[3], HalfStep, "the 1 s reload");
        }

        [UnityTest]
        public IEnumerator Heat_OverheatsAndRecoversOnTheAuthoredSteps()
        {
            var lasers = Template<Lasers>();
            lasers.projectilePrefab = Bolt();
            AddCooldown(lasers, secondsBetweenShots: 0.1f);
            lasers.gameObject.AddComponent<Heat>().Configure(maxHeat: 100f, heatPerShot: 30f, coolingRate: 100f,
                coolDownDelay: 0.1f, overheatPenaltyTime: 0.5f);

            var launches = new List<float>();
            yield return Fire(lasers, _ => true, seconds: 2.2f, launches);

            Assert.That(launches.Count, Is.GreaterThanOrEqualTo(5), "one overheating burst and the first shot after it");
            Assert.AreEqual(0.3f, launches[3] - launches[0], HalfStep, "30, 60, 90, then the overheating fourth shot");
            Assert.AreEqual(1.5f, launches[4] - launches[3], HalfStep, "0.5 s overheat penalty, then 100 heat at 100/s");
        }

        [UnityTest]
        public IEnumerator Charge_FiresOnTheAuthoredSteps()
        {
            var lasers = Template<ChargeLasers>();
            lasers.projectilePrefab = Bolt();
            AddCooldown(lasers, secondsBetweenShots: 0.1f);
            lasers.gameObject.AddComponent<ChargeTime>().Configure(chargeTime: 1f, minChargeToFire: 0.5f);

            var held = new List<float>();
            yield return Fire(lasers, _ => true, seconds: 2.2f, held);
            var tapped = new List<float>();
            yield return Fire(lasers, weapon => !weapon.CanFire(), seconds: 1.5f, tapped);

            Assert.That(held.Count, Is.GreaterThanOrEqualTo(2), "two full charges");
            Assert.AreEqual(1f, held[1] - held[0], HalfStep, "held: fires at full charge every 1 s");
            Assert.That(tapped.Count, Is.GreaterThanOrEqualTo(2), "two releases at minimum charge");
            Assert.AreEqual(0.5f + Time.fixedDeltaTime, tapped[1] - tapped[0], HalfStep,
                "released once firable: half charge, then the release step");
        }
    }
}
#endif
