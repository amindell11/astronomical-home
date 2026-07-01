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
    }
}
