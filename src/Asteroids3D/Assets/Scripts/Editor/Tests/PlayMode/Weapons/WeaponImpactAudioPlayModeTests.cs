#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using Combat.Projectiles;
using Combat.Weapons.Arsenal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons
{
    [Category("Audio")]
    public class WeaponImpactAudioPlayModeTests : WeaponAudioFixture
    {
        private const string RailgunPrefabPath = "Assets/Prefabs/Weapons/Railgun.prefab";
        private const string GrenadesPrefabPath = "Assets/Prefabs/Weapons/Grenades.prefab";
        private const string RailgunImpact = "event:/Weapons/Railgun/Impact";
        private const string GrenadeDetonate = "event:/Weapons/Grenade/Detonate";

        [UnityTest]
        public IEnumerator RailgunBeamHit_PlaysOneImpactAtTheHitPoint()
        {
            var railgun = Mount<Railguns>(RailgunPrefabPath);
            Target(Ahead(railgun, 5f));
            railgun.Charge.Configure(chargeTime: Time.fixedDeltaTime, minChargeToFire: 1f);
            var impact = Fresh(RailgunImpact);
            Vector3? hitPoint = null;
            railgun.Hit += (point, _) => hitPoint = point;

            Hold(railgun, 1);
            Assert.IsTrue(hitPoint.HasValue, "one held step reaches full charge, fires and hits the target");
            yield return Flush();

            var instances = Instances(impact);
            Debug.Log($"[FMOD] {RailgunImpact} instances={instances.Length} hit={hitPoint}");
            Assert.AreEqual(1, instances.Length, "one impact per hit");
            Assert.Less(Vector3.Distance(Position(instances[0]), hitPoint.Value), 0.01f, "played at the hit point");
            AssertPlaying(instances[0], "the impact still sounds a frame later");
        }

        [UnityTest]
        public IEnumerator RailgunBeamMiss_PlaysNoImpact()
        {
            var railgun = Mount<Railguns>(RailgunPrefabPath);
            railgun.Charge.Configure(chargeTime: Time.fixedDeltaTime, minChargeToFire: 1f);
            var impact = Fresh(RailgunImpact);
            var fired = 0;
            railgun.OnFire += () => fired++;
            var hits = 0;
            railgun.Hit += (point, target) => hits++;

            Hold(railgun, 1);
            Assert.AreEqual(1, fired, "the shot fired");
            Assert.AreEqual(0, hits, "nothing in range: the beam runs to max range");
            yield return Flush();

            Debug.Log($"[FMOD] {RailgunImpact} instances={Instances(impact).Length} after a miss");
            Assert.AreEqual(0, Instances(impact).Length, "no impact without a hit");
        }

        [UnityTest]
        public IEnumerator GrenadeArrival_PlaysOneDetonationWhereItDetonates()
        {
            var grenades = Mount<Grenades>(GrenadesPrefabPath);
            var detonate = Fresh(GrenadeDetonate);
            var grenade = grenades.Fire(Ahead(grenades, 5f), Projectiles) as Grenade;
            Assert.IsNotNull(grenade, "shot refused");
            Vector3? detonatedAt = null;
            grenade.OnDetonated += at => detonatedAt = at;

            for (var i = 0; i < 200 && !detonatedAt.HasValue; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(detonatedAt.HasValue, "the charge detonates on arrival");
            yield return Flush();

            var instances = Instances(detonate);
            Debug.Log($"[FMOD] {GrenadeDetonate} instances={instances.Length} detonatedAt={detonatedAt}");
            Assert.AreEqual(1, instances.Length, "one detonation per charge");
            Assert.Less(Vector3.Distance(Position(instances[0]), detonatedAt.Value), 0.01f, "played where the charge detonated");
            AssertPlaying(instances[0], "the detonation still sounds a frame later");
        }
    }
}
#endif
