using Combat.Conditions;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Weapons")]
    public class HeatEditModeTests
    {
        [Test]
        public void ProcessFire_ReachingMaxHeat_SetsOverheatedAndPreventsFiring()
        {
            var go = new GameObject("HeatTest");
            try
            {
                var heat = go.AddComponent<Heat>();

                // Default Heat values: max=100, perShot=25.
                for (var i = 0; i < 4; i++)
                {
                    heat.ProcessFire();
                }

                Assert.IsTrue(heat.Overheated);
                Assert.IsFalse(heat.CanFire());
                Assert.AreEqual(1f, heat.HeatPct, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WouldOverheatOnNextShot_WhenNextShotReachesExactlyMax_ReturnsFalse()
        {
            var go = new GameObject("HeatTest");
            try
            {
                var heat = go.AddComponent<Heat>();

                for (var i = 0; i < 3; i++)
                {
                    heat.ProcessFire();
                }

                // Default setup is 100 max / 25 per shot. At 75, next shot reaches exactly 100.
                // Expected behavior: reaching max is allowed; only subsequent shots should be blocked.
                Assert.IsFalse(heat.WouldOverheatOnNextShot());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── Cooldown-cycle logic (was covered by the slow HeatPlayModeTests; now driven by Heat.Tick
        //    against its internal clock, so these run at zero wall-time in EditMode) ───────────────

        /// <summary>Creates a Heat configured for fast, deterministic overheat/cooldown cycles.</summary>
        private static Heat NewConfiguredHeat()
        {
            var heat = new GameObject("HeatTest").AddComponent<Heat>();
            heat.Configure(maxHeat: 10f, heatPerShot: 5f, coolingRate: 20f,
                           coolDownDelay: 0.01f, overheatPenaltyTime: 0.01f);
            return heat;
        }

        /// <summary>Advances Heat's internal clock in fixed steps until it recovers or times out.</summary>
        private static void TickUntilNotOverheated(Heat heat, float maxSimSeconds = 2f, float dt = 0.02f)
        {
            var t = 0f;
            while (heat.Overheated && t < maxSimSeconds)
            {
                heat.Tick(dt);
                t += dt;
            }
        }

        [Test]
        public void RepeatedOverheatCooldownCycles_RecoversEachCycle_NoStuckState()
        {
            var heat = NewConfiguredHeat();
            try
            {
                const int cycles = 15;
                for (var cycle = 0; cycle < cycles; cycle++)
                {
                    var fireGuard = 0;
                    while (heat.CanFire() && fireGuard++ < 20)
                        heat.ProcessFire();

                    Assert.IsTrue(heat.Overheated, $"Cycle {cycle}: expected to overheat");

                    TickUntilNotOverheated(heat);

                    Assert.IsFalse(heat.Overheated,
                        $"Cycle {cycle}: heat stayed overheated too long (possible stuck state)");
                }
            }
            finally
            {
                if (heat) Object.DestroyImmediate(heat.gameObject);
            }
        }

        [Test]
        public void Overheated_PersistsUntilHeatReachesZero()
        {
            var heat = NewConfiguredHeat();
            try
            {
                heat.ProcessFire();
                heat.ProcessFire();
                Assert.IsTrue(heat.Overheated);

                var cooldownStarted = false;
                heat.OnCooldownStart += () => cooldownStarted = true;

                // Below max - perShot, the point a hysteresis exit would release the lockout.
                var guard = 0f;
                while (heat.CurrentHeat > 4f && guard < 2f)
                {
                    heat.Tick(0.02f);
                    guard += 0.02f;
                }

                Assert.Less(heat.CurrentHeat, heat.MaxHeat - heat.HeatPerShot,
                    "Setup failed to cool below the hysteresis-exit point");
                Assert.Greater(heat.CurrentHeat, 0f);
                Assert.IsTrue(heat.Overheated, "Lockout must persist while any heat remains");
                Assert.IsFalse(heat.CanFire());
                Assert.IsFalse(cooldownStarted);

                TickUntilNotOverheated(heat);

                Assert.AreEqual(0f, heat.CurrentHeat, "Lockout exits only at zero heat");
                Assert.IsFalse(heat.Overheated);
                Assert.IsTrue(heat.CanFire());
                Assert.IsTrue(cooldownStarted, "OnCooldownStart marks the zero-heat exit");
            }
            finally
            {
                if (heat) Object.DestroyImmediate(heat.gameObject);
            }
        }

        [Test]
        public void SpammingProcessFireWhileOverheated_ClampsAtMax_AndRecoversWhenSpamStops()
        {
            var heat = NewConfiguredHeat();
            try
            {
                heat.ProcessFire();
                heat.ProcessFire();
                Assert.IsTrue(heat.Overheated);

                // An incorrect caller that keeps firing while overheated must not push heat past max,
                // and keeps refreshing lastShotTime so cooldown cannot start.
                for (var i = 0; i < 45; i++)
                {
                    heat.ProcessFire();
                    heat.Tick(0.01f);
                }

                Assert.LessOrEqual(heat.CurrentHeat, heat.MaxHeat + 0.0001f,
                    "Heat must remain clamped at max (no heat debt beyond max)");
                Assert.IsTrue(heat.Overheated, "Continuous spam should keep it overheated");

                // Once spam stops, cooldown recovers.
                TickUntilNotOverheated(heat);
                Assert.IsFalse(heat.Overheated, "Heat should recover after spam stops");
            }
            finally
            {
                if (heat) Object.DestroyImmediate(heat.gameObject);
            }
        }

        [Test]
        public void Cooldown_DoesNotStartUntilDelayElapses()
        {
            var heat = new GameObject("HeatTest").AddComponent<Heat>();
            try
            {
                // Slow cooling, long delay, so we can observe the delay gate precisely.
                heat.Configure(maxHeat: 100f, heatPerShot: 20f, coolingRate: 50f,
                               coolDownDelay: 0.5f, overheatPenaltyTime: 0.5f);
                heat.ProcessFire(); // heat = 20
                var afterShot = heat.CurrentHeat;

                // Tick within the cooldown delay: no cooling yet.
                heat.Tick(0.2f);
                Assert.AreEqual(afterShot, heat.CurrentHeat, 0.0001f,
                    "Heat must not cool before coolDownDelay elapses");

                // Cross the delay: cooling begins.
                heat.Tick(0.4f); // clock now 0.6 > 0.5 delay
                Assert.Less(heat.CurrentHeat, afterShot, "Heat must cool once the delay has elapsed");
            }
            finally
            {
                if (heat) Object.DestroyImmediate(heat.gameObject);
            }
        }
    }
}
