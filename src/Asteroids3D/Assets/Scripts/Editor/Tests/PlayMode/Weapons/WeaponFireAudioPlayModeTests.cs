#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons
{
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
            // Read now: the weapon cools from the next frame; the parameter holds the shot's heat.
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
        public IEnumerator RipperShots_SendFirstShot_OnlyAfterAFiringGap()
        {
            var savedCaptureDelta = Time.captureDeltaTime;
            // One fixed step per frame, so a flushed frame never skips past the cooldown.
            Time.captureDeltaTime = Time.fixedDeltaTime;
            try
            {
                var rippers = Mount<Rippers>(RippersPrefabPath);
                var interval = rippers.GetComponent<Cooldown>().SecondsBetweenShots;
                var times = new List<float>();
                var firstShots = new List<float>();

                yield return new WaitForFixedUpdate();
                yield return RipperShot(rippers, times, firstShots);
                yield return UntilFirable(rippers);
                yield return RipperShot(rippers, times, firstShots);
                yield return UntilFirable(rippers);
                yield return new WaitForFixedUpdate();
                yield return RipperShot(rippers, times, firstShots);

                Debug.Log($"[FMOD] {RipperFire} shots at {string.Join(", ", times)} FirstShot={string.Join(", ", firstShots)}");
                var halfStep = Time.fixedDeltaTime * 0.5f;
                Assert.AreEqual(interval, times[1] - times[0], halfStep, "the second shot is held fire");
                Assert.AreEqual(interval + Time.fixedDeltaTime, times[2] - times[1], halfStep, "the third shot is one step late");
                Assert.That(firstShots, Is.EqualTo(new[] { 1f, 0f, 1f }), "opening shot, held fire, shot after a gap");
            }
            finally
            {
                Time.captureDeltaTime = savedCaptureDelta;
            }
        }

        // Fires on the current fixed step and records the step and the shot's FirstShot value.
        private IEnumerator RipperShot(Rippers rippers, List<float> times, List<float> firstShots)
        {
            var fire = Fresh(RipperFire);
            Assert.IsNotNull(rippers.Fire(Ahead(rippers, 10f), Projectiles), "shot refused");
            times.Add(Time.fixedTime);
            yield return Flush();

            var instances = Instances(fire);
            Assert.AreEqual(1, instances.Length, "ripper fire instances after one shot");
            firstShots.Add(Parameter(instances[0], "FirstShot"));
        }

        private static IEnumerator UntilFirable(Rippers rippers)
        {
            do
            {
                yield return new WaitForFixedUpdate();
            } while (!rippers.CanFire());
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
