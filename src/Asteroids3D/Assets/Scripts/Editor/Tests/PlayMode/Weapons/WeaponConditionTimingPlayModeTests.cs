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
    /// Timed weapon conditions run through real fixed steps with one rendered frame per step.
    /// Single-event checks allow half a step; sustained-fire checks bound cumulative cadence drift.
    /// </summary>
    [Category("Weapons")]
    public class WeaponConditionTimingPlayModeTests : PlayModeWorldFixture
    {
        private static readonly float[] SustainedIntervals = { 0.1f, 0.05f, 0.075f };
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
        public IEnumerator Cooldown_SustainedFirePreservesAverageCadence(
            [ValueSource(nameof(SustainedIntervals))] float interval)
        {
            var rippers = Template<Rippers>();
            rippers.projectilePrefab = Bolt();
            AddCooldown(rippers, interval);
            rippers.gameObject.AddComponent<Rounds>().Configure(maxAmmo: 100, reloadTime: 1f);

            var launches = new List<float>();
            yield return Fire(rippers, _ => true, seconds: 22f * interval, launches);

            Assert.That(launches.Count, Is.GreaterThanOrEqualTo(21));
            Assert.AreEqual(20f * interval, launches[20] - launches[0], Time.fixedDeltaTime,
                "twenty shot intervals must retain the configured average cadence");
        }

        [UnityTest]
        public IEnumerator Cooldown_AfterPauseFiresImmediatelyWithoutCatchingUp()
        {
            const float interval = 0.05f;
            const int resumeStep = 26;
            var rippers = Template<Rippers>();
            rippers.projectilePrefab = Bolt();
            AddCooldown(rippers, interval);
            rippers.gameObject.AddComponent<Rounds>().Configure(maxAmmo: 100, reloadTime: 1f);

            var launches = new List<float>();
            var step = 0;
            yield return Fire(rippers, _ => ++step == 1 || step >= resumeStep, seconds: 1.8f, launches);

            Assert.That(launches.Count, Is.GreaterThanOrEqualTo(22));
            Assert.AreEqual((resumeStep - 1) * Time.fixedDeltaTime, launches[1] - launches[0], HalfStep,
                "the first resumed trigger fires immediately");
            Assert.AreEqual(20f * interval, launches[21] - launches[1], Time.fixedDeltaTime,
                "resumed fire starts a fresh cadence without catching up missed shots");
            for (var shot = 2; shot <= 21; shot++)
                Assert.That(launches[shot] - launches[shot - 1], Is.GreaterThan(interval - Time.fixedDeltaTime));
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
    }
}
#endif
