using Combat.Conditions;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Regression")]
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
        public void WouldOverheatOnNextShot_WhenAtSeventyFivePercentHeat_ReturnsTrue()
        {
            var go = new GameObject("HeatTest");
            try
            {
                var heat = go.AddComponent<Heat>();

                for (var i = 0; i < 3; i++)
                {
                    heat.ProcessFire();
                }

                // At 75 heat with 25 heat/shot, next shot reaches max.
                // Characterization expectation for board bug: this should be treated as an overheat risk.
                Assert.IsTrue(heat.WouldOverheatOnNextShot());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
