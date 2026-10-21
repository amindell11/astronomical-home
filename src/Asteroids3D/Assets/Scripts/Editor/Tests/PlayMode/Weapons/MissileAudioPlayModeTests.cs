#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using Combat.Projectiles;
using Combat.Weapons.Arsenal;
using FMOD.Studio;
using FMODUnity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons
{
    [Category("Audio")]
    public class MissileAudioPlayModeTests : WeaponAudioFixture
    {
        private const string MissilesPrefabPath = "Assets/Prefabs/Weapons/Missiles.prefab";
        private const string MissileFlight = "event:/Weapons/Missile/Flight";
        private const string MissileDetonate = "event:/Weapons/Missile/Detonate";

        [UnityTest]
        public IEnumerator Launch_StartsOneFlightInstance_FollowingSpeedAndVelocity()
        {
            var flight = Fresh(MissileFlight);
            var missile = Launch();
            var launchSpeed = missile.NormalizedSpeed;

            for (var i = 0; i < 10; i++)
                yield return new WaitForFixedUpdate();
            yield return Settled();

            var instances = Instances(flight);
            Assert.AreEqual(1, instances.Length, "one flight instance per launch");
            var speed = Parameter(instances[0], "Speed");
            var velocity = Velocity(instances[0]);
            var bodyVelocity = missile.GetComponent<Rigidbody>().linearVelocity;
            Debug.Log($"[FMOD] {MissileFlight} state={State(instances[0])} speed={speed} normalizedSpeed={missile.NormalizedSpeed} launchSpeed={launchSpeed} velocity={velocity} body={bodyVelocity}");
            AssertFlying(instances[0], "the flight sounds while the missile flies");
            Assert.Greater(missile.NormalizedSpeed, launchSpeed, "the missile accelerated since launch");
            Assert.AreEqual(missile.NormalizedSpeed, speed, 1e-4f, "Speed follows the missile's normalized speed");
            Assert.Less(Vector3.Distance(velocity, bodyVelocity), 0.01f, "Doppler reads the missile's velocity");
        }

        [UnityTest]
        public IEnumerator Detonation_StopsTheFlight_AndPlaysOneDetonationWhereItDetonates()
        {
            var flight = Fresh(MissileFlight);
            var detonate = Fresh(MissileDetonate);
            var missile = Launch();
            Vector3? detonatedAt = null;
            missile.OnDetonated += at => detonatedAt = at;

            for (var i = 0; i < 5; i++)
                yield return new WaitForFixedUpdate();
            yield return Flush();
            AssertOneFlying(flight, "the flight sounds before the detonation");

            missile.TakeDamage(default);
            Assert.IsTrue(detonatedAt.HasValue, "a shot-down missile detonates");
            yield return Flush();

            var flights = Instances(flight);
            var detonations = Instances(detonate);
            Debug.Log($"[FMOD] {MissileFlight} instances={flights.Length} {(flights.Length > 0 ? State(flights[0]).ToString() : "-")}; {MissileDetonate} instances={detonations.Length} detonatedAt={detonatedAt}");
            AssertNoneFlying(flights, "the detonation stops the flight");
            Assert.AreEqual(1, detonations.Length, "one detonation per missile");
            Assert.Less(Vector3.Distance(Position(detonations[0]), detonatedAt.Value), 0.01f, "played where the missile detonated");
            AssertPlaying(detonations[0], "the detonation still sounds a frame later");
        }

        [UnityTest]
        public IEnumerator ReturnToPoolWithoutDetonating_LeavesNoFlightPlaying()
        {
            var flight = Fresh(MissileFlight);
            var detonate = Fresh(MissileDetonate);
            var missile = Launch();
            var detonated = false;
            missile.OnDetonated += _ => detonated = true;

            yield return Flush();
            AssertOneFlying(flight, "the flight sounds before the return");

            missile.ReturnToPoolImmediate();
            Assert.IsFalse(missile.gameObject.activeSelf, "the missile is back in its pool");
            yield return Flush();

            var flights = Instances(flight);
            Debug.Log($"[FMOD] {MissileFlight} instances={flights.Length} {(flights.Length > 0 ? State(flights[0]).ToString() : "-")} after a pool return");
            Assert.IsFalse(detonated, "a pool return is not a detonation");
            AssertNoneFlying(flights, "the pool return stops the flight");
            Assert.AreEqual(0, Instances(detonate).Length, "no detonation sound without a detonation");
        }

        private Missile Launch()
        {
            var missiles = Mount<Missiles>(MissilesPrefabPath);
            var missile = missiles.Fire(Ahead(missiles, 20f), Projectiles) as Missile;
            Assert.IsNotNull(missile, "launch refused");
            return missile;
        }

        // Yield first: Update feeds this frame's Speed, and the flush makes it readable.
        private static IEnumerator Settled()
        {
            yield return null;
            RuntimeManager.StudioSystem.flushCommands();
        }

        private static Vector3 Velocity(EventInstance instance)
        {
            instance.get3DAttributes(out var attributes);
            return new Vector3(attributes.velocity.x, attributes.velocity.y, attributes.velocity.z);
        }

        private static void AssertOneFlying(EventDescription description, string why)
        {
            var instances = Instances(description);
            Assert.AreEqual(1, instances.Length, why);
            AssertFlying(instances[0], why);
        }

        private static void AssertFlying(EventInstance instance, string why) =>
            Assert.That(State(instance), Is.EqualTo(PLAYBACK_STATE.PLAYING).Or.EqualTo(PLAYBACK_STATE.STARTING).Or.EqualTo(PLAYBACK_STATE.SUSTAINING), why);

        private static void AssertNoneFlying(EventInstance[] instances, string why)
        {
            foreach (var instance in instances)
                Assert.That(State(instance), Is.EqualTo(PLAYBACK_STATE.STOPPING).Or.EqualTo(PLAYBACK_STATE.STOPPED), why);
        }
    }
}
#endif
