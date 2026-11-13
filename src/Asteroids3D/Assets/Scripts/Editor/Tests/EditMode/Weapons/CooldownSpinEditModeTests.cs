using Combat.Weapons.Conditions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Weapons
{
    [Category("Weapons")]
    public class CooldownSpinEditModeTests
    {
        private const float Dt = 0.02f;
        private const float FireRate = 0.075f;
        private const float ColdFireRate = 0.2f;
        private const float SpinUpTime = 1.5f;
        private const float SpinDownTime = 0.4f;

        [Test]
        public void NoSpinUp_FiresAtAuthoredRateFromTheStart()
        {
            var go = new GameObject("CooldownSpin");
            try
            {
                var cooldown = go.AddComponent<Cooldown>();
                Configure(cooldown, spinUpTime: 0f);

                Assert.AreEqual(1f, cooldown.Spin);
                Assert.AreEqual(FireRate, cooldown.SecondsBetweenShots, 1e-5f);

                Hold(cooldown, 0.5f);
                Assert.AreEqual(1f, cooldown.Spin);
                Assert.AreEqual(FireRate, cooldown.SecondsBetweenShots, 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void HeldFire_RampsFromColdIntervalToFullSpin()
        {
            var go = new GameObject("CooldownSpin");
            try
            {
                var cooldown = go.AddComponent<Cooldown>();
                Configure(cooldown, SpinUpTime);

                Assert.AreEqual(ColdFireRate, cooldown.SecondsBetweenShots, 1e-4f);

                Hold(cooldown, 1.6f);
                Assert.AreEqual(1f, cooldown.Spin, 1e-4f);
                Assert.AreEqual(FireRate, cooldown.SecondsBetweenShots, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Release_WindsDownToCold()
        {
            var go = new GameObject("CooldownSpin");
            try
            {
                var cooldown = go.AddComponent<Cooldown>();
                Configure(cooldown, SpinUpTime);
                Hold(cooldown, 1.6f);
                Assert.AreEqual(1f, cooldown.Spin, 1e-4f);

                Idle(cooldown, 0.2f);
                Assert.Greater(cooldown.Spin, 0f);
                Assert.Less(cooldown.Spin, 1f);

                Idle(cooldown, 0.3f);
                Assert.AreEqual(0f, cooldown.Spin, 1e-4f);
                Assert.AreEqual(ColdFireRate, cooldown.SecondsBetweenShots, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TimeWithoutShots_LeavesSpinCold()
        {
            var go = new GameObject("CooldownSpin");
            try
            {
                var cooldown = go.AddComponent<Cooldown>();
                Configure(cooldown, SpinUpTime);

                Idle(cooldown, 3f);
                Assert.AreEqual(0f, cooldown.Spin);
                Assert.AreEqual(ColdFireRate, cooldown.SecondsBetweenShots, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FeatheredShots_BuySpinWithHeat_IdleTimeDoesNot()
        {
            var spent = Feather(shoot: true);
            try
            {
                Assert.Greater(spent.cooldown.Spin, 0.2f);
                Assert.IsTrue(spent.heat.CurrentHeat > 0f || spent.heat.Overheated);
            }
            finally
            {
                Object.DestroyImmediate(spent.cooldown.gameObject);
            }

            var idle = Feather(shoot: false);
            try
            {
                Assert.AreEqual(0f, idle.cooldown.Spin);
                Assert.AreEqual(0f, idle.heat.CurrentHeat);
            }
            finally
            {
                Object.DestroyImmediate(idle.cooldown.gameObject);
            }
        }

        private static void Configure(Cooldown cooldown, float spinUpTime)
        {
            using var serialized = new SerializedObject(cooldown);
            serialized.FindProperty("fireRate").floatValue = FireRate;
            serialized.FindProperty("coldFireRate").floatValue = ColdFireRate;
            serialized.FindProperty("spinUpTime").floatValue = spinUpTime;
            serialized.FindProperty("spinDownTime").floatValue = SpinDownTime;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            cooldown.Reset();
        }

        private static void Hold(Cooldown cooldown, float seconds)
        {
            var steps = Mathf.RoundToInt(seconds / Dt);
            for (var i = 0; i < steps; i++)
            {
                if (cooldown.CanFire()) cooldown.ProcessFire();
                cooldown.Tick(Dt);
            }
        }

        private static void Idle(Cooldown cooldown, float seconds)
        {
            var steps = Mathf.RoundToInt(seconds / Dt);
            for (var i = 0; i < steps; i++) cooldown.Tick(Dt);
        }

        // Default heat locks out after four shots, before a 1.5s spin-up can pass one half.
        private static (Cooldown cooldown, Heat heat) Feather(bool shoot)
        {
            var go = new GameObject("CooldownSpin");
            var cooldown = go.AddComponent<Cooldown>();
            var heat = go.AddComponent<Heat>();
            using (var serialized = new SerializedObject(heat))
            {
                serialized.FindProperty("heatPerShot").floatValue = 1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Configure(cooldown, SpinUpTime);

            var steps = Mathf.RoundToInt(3f / Dt);
            var holdSteps = Mathf.RoundToInt(0.3f / Dt);
            var releaseSteps = Mathf.RoundToInt(0.15f / Dt);
            var holding = true;
            var phase = 0;
            for (var i = 0; i < steps; i++)
            {
                if (shoot && holding && cooldown.CanFire() && heat.CanFire())
                {
                    cooldown.ProcessFire();
                    heat.ProcessFire();
                }

                cooldown.Tick(Dt);
                heat.Tick(Dt);
                phase++;
                var span = holding ? holdSteps : releaseSteps;
                if (phase < span) continue;
                phase = 0;
                holding = !holding;
            }

            return (cooldown, heat);
        }
    }
}
