using Combat.Projectiles;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Weapons
{
    [TestFixture]
    [Category("Weapons")]
    public class GunRoleEditModeTests
    {
        private const string LasersPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const string TurboLasersPath = "Assets/Prefabs/Weapons/TurboLasers.prefab";
        private const float Dt = 0.02f;

        [Test]
        public void Laser_FiresSlowerAndHotterAtTheSameDamage()
        {
            var lasers = Load<Lasers>(LasersPath);
            Assert.AreEqual(0.4f, lasers.GetComponent<Cooldown>().SecondsBetweenShots, 1e-4f);
            Assert.AreEqual(15f, lasers.Heat.HeatPerShot, 1e-4f);
            Assert.AreEqual(20f, lasers.Damage, 1e-4f);
        }

        [Test]
        public void Ripper_ReachesThirtyUnits_AndAiOpensFireInsideIt()
        {
            var slug = Load<Laser>("Assets/Prefabs/Weapons/Projectiles/RipperSlug.prefab");
            Assert.AreEqual(30f, slug.MaxDistance, 1e-4f);
            Assert.LessOrEqual(Load<Rippers>("Assets/Prefabs/Weapons/Rippers.prefab").FireRange, slug.MaxDistance);
        }

        [Test]
        public void TurboLaser_HitsHarderAndFurtherThanTheRipper()
        {
            var bolt = Load<Laser>("Assets/Prefabs/Weapons/Projectiles/TurboLaser.prefab");
            Assert.AreEqual(7f, bolt.Damage, 1e-4f);
            Assert.AreEqual(45f, bolt.MaxDistance, 1e-4f);
        }

        [Test]
        public void TurboLaser_HeldBurstSpinsUpThenOverheats_AndLocksOutUntilCold()
        {
            var turbo = Object.Instantiate(Load<Lasers>(TurboLasersPath).gameObject);
            try
            {
                var cooldown = turbo.GetComponent<Cooldown>();
                var heat = turbo.GetComponent<Heat>();
                cooldown.Reset();
                heat.Reset();
                Assert.AreEqual(0.2f, cooldown.SecondsBetweenShots, 1e-4f, "starts cold");

                var maxSpin = 0f;
                for (var t = 0f; t < 5f && !heat.Overheated; t += Dt)
                {
                    if (cooldown.CanFire() && heat.CanFire())
                    {
                        cooldown.ProcessFire();
                        heat.ProcessFire();
                    }
                    cooldown.Tick(Dt);
                    heat.Tick(Dt);
                    maxSpin = Mathf.Max(maxSpin, cooldown.Spin);
                }

                Assert.IsTrue(heat.Overheated, "a held burst overheats");
                Assert.AreEqual(1f, maxSpin, 1e-4f, "the burst reaches full spin before it overheats");

                var lockout = 0f;
                while (!heat.CanFire() && lockout < 10f)
                {
                    cooldown.Tick(Dt);
                    heat.Tick(Dt);
                    lockout += Dt;
                }
                Assert.AreEqual(2.9f, lockout, 0.1f, "overheat lockout");
                Assert.AreEqual(0f, cooldown.Spin, 1e-4f, "spin wound down during the lockout");
            }
            finally
            {
                Object.DestroyImmediate(turbo);
            }
        }

        private static T Load<T>(string path) where T : Component
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, path);
            return asset;
        }
    }
}
