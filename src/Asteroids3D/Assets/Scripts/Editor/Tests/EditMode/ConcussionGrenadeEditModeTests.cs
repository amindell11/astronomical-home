using Combat.Projectile;
using Combat.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Weapons")]
    public class ConcussionGrenadeEditModeTests
    {
        // ── Wave falloff ──

        [Test]
        public void Falloff_IsFullAtCenter_ZeroAtMaxRadius_LinearBetween()
        {
            Assert.AreEqual(1f, ConcussionWave.Falloff(0f, 12f), 0.0001f);
            Assert.AreEqual(0.5f, ConcussionWave.Falloff(6f, 12f), 0.0001f);
            Assert.AreEqual(0f, ConcussionWave.Falloff(12f, 12f), 0.0001f);
        }

        [Test]
        public void Falloff_ClampsBeyondMaxRadius_AndHandlesDegenerateRadius()
        {
            Assert.AreEqual(0f, ConcussionWave.Falloff(20f, 12f), 0.0001f);
            Assert.AreEqual(0f, ConcussionWave.Falloff(1f, 0f), 0.0001f);
        }

        // ── Hangar stat line ──

        [Test]
        public void HangarStats_ReadsBlastAndMagazine_OffThePrefabAsset()
        {
            // The hangar evaluates HangarStats on the asset, where Awake never runs — this only
            // passes if the condition ref and projectile/wave chain are serialized, not looked up.
            var prefab = AssetDatabase.LoadAssetAtPath<Grenades>("Assets/Prefabs/Weapons/Grenades.prefab");
            Assert.IsNotNull(prefab, "Grenades weapon prefab must exist.");

            var stats = prefab.HangarStats;
            StringAssert.Contains("Blast 40 to 12u", stats);
            StringAssert.Contains("3 charges (reload 12s)", stats);
            StringAssert.Contains("Fuse 2.5s", stats);
        }

        // ── AI drop heuristic ──

        private static TargetingContext Context(float distance, float angle) => new()
        {
            distanceToTarget = distance,
            angleToTarget = angle,
            hasLineOfSight = false,
        };

        [Test]
        public void ShouldFire_DropsOnACloseTargetBehind()
        {
            var go = new GameObject("Grenades");
            try
            {
                var grenades = go.AddComponent<Grenades>();
                Assert.IsTrue(grenades.ShouldFire(Context(distance: 8f, angle: 170f)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ShouldFire_IgnoresTargetsAheadOrOutOfRange()
        {
            var go = new GameObject("Grenades");
            try
            {
                var grenades = go.AddComponent<Grenades>();
                Assert.IsFalse(grenades.ShouldFire(Context(distance: 8f, angle: 10f)),
                    "A target ahead is a gun target, not a drop target.");
                Assert.IsFalse(grenades.ShouldFire(Context(distance: 50f, angle: 170f)),
                    "A distant pursuer is out of drop range.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
