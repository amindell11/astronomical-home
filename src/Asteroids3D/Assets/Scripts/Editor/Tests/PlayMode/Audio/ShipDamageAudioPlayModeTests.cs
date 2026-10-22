#if UNITY_EDITOR && ASTRO_FMOD
using System;
using System.Collections;
using Damage;
using NUnit.Framework;
using Ships.Audio;
using Ships.Damage;
using Ships.Presentation;
using Ships.Registry;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Audio
{
    [Category("Audio")]
    public class ShipDamageAudioPlayModeTests : FmodEvidenceFixture
    {
        private const string RigPrefabPath = "Assets/Visuals/Ships/_Shared/ShipBaseRig.prefab";
        private const string ShieldHitEvent = "event:/Ships/ShieldHit";
        private const string HullHitEvent = "event:/Ships/HullHit";
        private const string DeathEvent = "event:/Ships/Death";
        private const string CollisionDeathEvent = "event:/Ships/CollisionDeath";

        private static readonly Vector3 ShipPosition = new(3f, 0f, -2f);

        private sealed class FakeDamage : IDamageEvents
        {
            public event Action<AppliedDamage> OnDamaged { add { } remove { } }
            public event Action<ShipId, DamageInfo> OnDeath;
            public Resource Health { get; } = new Resource(100f);
            public RegenResource Shield { get; } = new RegenResource(100f, 0f, 999f);

            public void Die(DamageKind kind) =>
                OnDeath?.Invoke(default, new DamageInfo(100f, kind, ShipId.Invalid, 0f, Vector3.zero, Vector3.zero));
        }

        private GameObject host;

        [TearDown]
        public override void TearDown()
        {
            if (host) Object.DestroyImmediate(host);
            base.TearDown();
        }

        // The base rig cannot instantiate alone, so its authored values go onto a lone component.
        private FakeDamage NewBoundDamageAudio()
        {
            var authored = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath).GetComponentInChildren<ShipDamageAudio>(true);
            Assert.IsNotNull(authored, "the rig prefab carries ShipDamageAudio");
            host = new GameObject("ShipDamageAudio");
            host.transform.position = ShipPosition;
            var audio = host.AddComponent<ShipDamageAudio>();
            ComponentUtility.CopyComponent(authored);
            ComponentUtility.PasteComponentValues(audio);
            var damage = new FakeDamage();
            audio.Bind(new ShipView(host.transform, damage, null, null, false));
            return damage;
        }

        [UnityTest]
        public IEnumerator ShieldDrop_PlaysOneShieldHitPerDrop_AtTheShip()
        {
            var shieldHit = Fresh(ShieldHitEvent);
            var hullHit = Fresh(HullHitEvent);
            var damage = NewBoundDamageAudio();

            damage.Shield.ApplyDamage(20f);
            yield return Flush();
            var instances = Instances(shieldHit);
            Debug.Log($"[FMOD] {ShieldHitEvent} instances={instances.Length} after one shield drop");
            Assert.AreEqual(1, instances.Length, "one shield hit per drop");
            Assert.Less(Vector3.Distance(Position(instances[0]), ShipPosition), 0.01f, "played at the ship");
            Assert.AreEqual(0, Instances(hullHit).Length, "a shield drop plays no hull hit");

            shieldHit = Fresh(ShieldHitEvent);
            damage.Shield.ApplyDamage(20f);
            yield return Flush();
            Debug.Log($"[FMOD] {ShieldHitEvent} instances={Instances(shieldHit).Length} after the second drop");
            Assert.AreEqual(1, Instances(shieldHit).Length, "one shield hit for the second drop");

            shieldHit = Fresh(ShieldHitEvent);
            damage.Shield.RestoreFraction(0.5f);
            yield return Flush();
            Assert.AreEqual(0, Instances(shieldHit).Length, "a recharge plays nothing");
        }

        [UnityTest]
        public IEnumerator HealthDrop_PlaysOneHullHitPerDrop_AtTheShip()
        {
            var hullHit = Fresh(HullHitEvent);
            var shieldHit = Fresh(ShieldHitEvent);
            var damage = NewBoundDamageAudio();

            damage.Health.ApplyDamage(20f);
            yield return Flush();
            var instances = Instances(hullHit);
            Debug.Log($"[FMOD] {HullHitEvent} instances={instances.Length} after one health drop");
            Assert.AreEqual(1, instances.Length, "one hull hit per drop");
            Assert.Less(Vector3.Distance(Position(instances[0]), ShipPosition), 0.01f, "played at the ship");
            Assert.AreEqual(0, Instances(shieldHit).Length, "a health drop plays no shield hit");

            hullHit = Fresh(HullHitEvent);
            damage.Health.ApplyDamage(20f);
            yield return Flush();
            Debug.Log($"[FMOD] {HullHitEvent} instances={Instances(hullHit).Length} after the second drop");
            Assert.AreEqual(1, Instances(hullHit).Length, "one hull hit for the second drop");

            hullHit = Fresh(HullHitEvent);
            damage.Health.RestoreFraction(0.5f);
            yield return Flush();
            Assert.AreEqual(0, Instances(hullHit).Length, "a repair plays nothing");
        }

        [UnityTest]
        public IEnumerator LaserKill_PlaysOneDeath_AndNoCollisionDeath() =>
            ExactlyOneDeath(DamageKind.Laser, DeathEvent, CollisionDeathEvent);

        [UnityTest]
        public IEnumerator CollisionKill_PlaysOneCollisionDeath_AndNoDeath() =>
            ExactlyOneDeath(DamageKind.Collision, CollisionDeathEvent, DeathEvent);

        private IEnumerator ExactlyOneDeath(DamageKind killingBlow, string expectedEvent, string otherEvent)
        {
            var expected = Fresh(expectedEvent);
            var other = Fresh(otherEvent);
            var damage = NewBoundDamageAudio();

            damage.Die(killingBlow);
            yield return Flush();
            var played = Instances(expected);
            var otherCount = Instances(other).Length;
            Debug.Log($"[FMOD] killing blow {killingBlow}: {expectedEvent} instances={played.Length}, {otherEvent} instances={otherCount}");
            Assert.AreEqual(1, played.Length, $"a {killingBlow} kill plays one {expectedEvent}");
            Assert.AreEqual(0, otherCount, $"a {killingBlow} kill plays no {otherEvent}");
            Assert.Less(Vector3.Distance(Position(played[0]), ShipPosition), 0.01f, "played where the ship died");
            AssertPlaying(played[0], "the death still sounds a frame later");
        }

        [UnityTest]
        public IEnumerator HiddenRig_PlaysNoDamageOrDeath()
        {
            var descriptions = new[]
            {
                Fresh(ShieldHitEvent), Fresh(HullHitEvent), Fresh(DeathEvent), Fresh(CollisionDeathEvent),
            };
            var damage = NewBoundDamageAudio();
            host.SetActive(false);

            damage.Shield.ApplyDamage(50f);
            damage.Health.ApplyDamage(50f);
            damage.Die(DamageKind.Laser);
            damage.Die(DamageKind.Collision);
            yield return Flush();

            foreach (var description in descriptions)
            {
                description.getPath(out var path);
                var count = Instances(description).Length;
                Debug.Log($"[FMOD] {path} instances={count} from a hidden rig");
                Assert.AreEqual(0, count, $"a hidden rig plays no {path}");
            }
        }
    }
}
#endif
