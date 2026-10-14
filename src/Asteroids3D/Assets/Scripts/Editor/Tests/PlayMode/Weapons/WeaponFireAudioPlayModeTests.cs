#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using Combat.Weapons.Arsenal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons
{
    /// <summary>The fire one-shots of the families ported after the laser: one instance per shot, from the family's own event.</summary>
    [Category("Audio")]
    public class WeaponFireAudioPlayModeTests : WeaponAudioFixture
    {
        private const string TurboLasersPrefabPath = "Assets/Prefabs/Weapons/TurboLasers.prefab";
        private const string RippersPrefabPath = "Assets/Prefabs/Weapons/Rippers.prefab";
        private const string GrenadesPrefabPath = "Assets/Prefabs/Weapons/Grenades.prefab";
        private const string LaserFire = "event:/Weapons/Laser/Fire";
        private const string TurboLaserFire = "event:/Weapons/TurboLaser/Fire";
        private const string RipperFire = "event:/Weapons/Ripper/Fire";
        private const string GrenadeFire = "event:/Weapons/Grenade/Fire";

        [UnityTest]
        public IEnumerator TurboLaserShot_StartsTheTurboLaserEvent_WithWeaponHeat()
        {
            var lasers = Mount<Lasers>(TurboLasersPrefabPath);
            var turbo = Fresh(TurboLaserFire);
            var laser = Fresh(LaserFire);

            Assert.IsNotNull(lasers.Fire(Ahead(lasers, 10f), Projectiles), "shot refused");
            // The weapon cools from the next frame on; the parameter holds the heat as the shot left it.
            var heatAfterShot = lasers.Heat.HeatPct;
            Assert.Greater(heatAfterShot, 0f, "a shot leaves heat behind");
            yield return Flush();

            var instances = Instances(turbo);
            Debug.Log($"[FMOD] {TurboLaserFire} instances={instances.Length}; {LaserFire} instances={Instances(laser).Length}");
            Assert.AreEqual(1, instances.Length, "turbo laser fire instances after one shot");
            Assert.AreEqual(0, Instances(laser).Length, "the variant overrides the base prefab's event");
            var heat = Parameter(instances[0], "Heat");
            Debug.Log($"[FMOD] heat={heat} weaponHeat={heatAfterShot} state={State(instances[0])}");
            Assert.AreEqual(heatAfterShot, heat, 1e-4f);
            AssertPlaying(instances[0], "the shot still sounds a frame later");
        }

        [UnityTest]
        public IEnumerator RipperShot_StartsOneFireInstance()
        {
            var rippers = Mount<Rippers>(RippersPrefabPath);
            var fire = Fresh(RipperFire);

            Assert.IsNotNull(rippers.Fire(Ahead(rippers, 10f), Projectiles), "shot refused");
            yield return Flush();

            var instances = Instances(fire);
            Debug.Log($"[FMOD] {RipperFire} instances={instances.Length}");
            Assert.AreEqual(1, instances.Length, "ripper fire instances after one shot");
            AssertPlaying(instances[0], "the shot still sounds a frame later");
        }

        [UnityTest]
        public IEnumerator GrenadeShot_StartsOneFireInstance()
        {
            var grenades = Mount<Grenades>(GrenadesPrefabPath);
            var fire = Fresh(GrenadeFire);

            Assert.IsNotNull(grenades.Fire(Ahead(grenades, 20f), Projectiles), "shot refused");
            yield return Flush();

            var instances = Instances(fire);
            Debug.Log($"[FMOD] {GrenadeFire} instances={instances.Length}");
            Assert.AreEqual(1, instances.Length, "grenade fire instances after one shot");
            AssertPlaying(instances[0], "the shot still sounds a frame later");
        }
    }
}
#endif
