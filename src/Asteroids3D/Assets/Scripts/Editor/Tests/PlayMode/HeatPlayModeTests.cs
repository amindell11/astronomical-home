using System.Collections;
using System.Reflection;
using Combat.Conditions;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    [Category("Weapons")]
    public class HeatPlayModeTests : PlayModeWorldFixture
    {
        private static void SetPrivateField(object obj, string fieldName, float value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private static Heat CreateConfiguredHeat(string name = "HeatPlayModeTest")
        {
            var go = new GameObject(name);
            var heat = go.AddComponent<Heat>();

            // Keep tests fast and deterministic.
            SetPrivateField(heat, "maxHeat", 10f);
            SetPrivateField(heat, "heatPerShot", 5f);
            SetPrivateField(heat, "coolingRate", 20f);
            SetPrivateField(heat, "coolDownDelay", 0.01f);
            SetPrivateField(heat, "overheatPenaltyTime", 0.01f);

            return heat;
        }

        [UnityTest]
        public IEnumerator RepeatedOverheatCooldownCycles_WithCanFireGuard_DoesNotGetStuckOverheated()
        {
            var heat = CreateConfiguredHeat();
            try
            {
                const int cycles = 15;
                for (var cycle = 0; cycle < cycles; cycle++)
                {
                    // Fire until overheat using normal guard pattern.
                    var fireGuard = 0;
                    while (heat.CanFire() && fireGuard++ < 20)
                    {
                        heat.ProcessFire();
                        yield return null;
                    }

                    Assert.IsTrue(heat.Overheated, $"Cycle {cycle}: expected to overheat");

                    // Keep trying to fire the way WeaponBase does (guarded by CanFire).
                    // This should not create any hidden heat debt.
                    var cooldownElapsed = 0f;
                    const float cooldownTimeout = 2f;
                    while (heat.Overheated && cooldownElapsed < cooldownTimeout)
                    {
                        if (heat.CanFire())
                            heat.ProcessFire();

                        yield return null;
                        cooldownElapsed += Time.deltaTime;
                    }

                    Assert.IsFalse(heat.Overheated,
                        $"Cycle {cycle}: heat stayed overheated too long (possible stuck state)");
                }
            }
            finally
            {
                if (heat != null)
                    Object.Destroy(heat.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator SpammingProcessFireWhileOverheated_DelaysCooldownButEventuallyRecoversWhenSpamStops()
        {
            var heat = CreateConfiguredHeat();
            try
            {
                // Reach overheat.
                heat.ProcessFire();
                heat.ProcessFire();
                Assert.IsTrue(heat.Overheated);

                // Simulate an incorrect caller that invokes ProcessFire even while overheated.
                // This should not increase heat above max, but it keeps refreshing _lastShotTime.
                for (var i = 0; i < 45; i++)
                {
                    heat.ProcessFire();
                    yield return null;
                }

                Assert.LessOrEqual(heat.CurrentHeat, heat.MaxHeat + 0.0001f,
                    "Heat must remain clamped at max (no heat debt beyond max)");

                // Once spam stops, cooldown should recover.
                var elapsed = 0f;
                const float timeout = 2f;
                while (heat.Overheated && elapsed < timeout)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }

                Assert.IsFalse(heat.Overheated,
                    "Heat should recover after spam stops (not permanently stuck)");
            }
            finally
            {
                if (heat != null)
                    Object.Destroy(heat.gameObject);
            }
        }
    }
}
