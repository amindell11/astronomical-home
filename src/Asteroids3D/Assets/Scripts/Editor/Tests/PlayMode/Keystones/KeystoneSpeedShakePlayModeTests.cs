using System.Collections;
using Cameras.Shake;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Keystones;
using Ships.Keystones.Kits;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Keystones
{
    [Category("Ships")]
    public class KeystoneSpeedShakePlayModeTests : PlayModeWorldFixture
    {
        private const string KitPrefabPath = "Assets/Prefabs/Keystones/Valis_Overdrive.prefab";

        private static readonly Kinematics Still = new(Vector2.zero, Vector2.zero, 0f, 0f, 0f);

        private GameObject kitHost;
        private Ship ship;
        private Overdrive kit;
        private CameraShakeProfile profileCopy;
        private Kinematics motion;
        private int raises;
        private int scales;
        private float scaledStrength;
        private int releases;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            ship = ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero);
            kit = SpawnKit(ship.transform);
            kit.Initialize(new KeystoneHost(ship.Rigidbody, 1f, ship.Lock));
            motion = Still;
            (raises, scales, scaledStrength, releases) = (0, 0, 0f, 0);
            ship.Movement.Initialize(ship.Stats, () => motion, kit, null);
            Assert.Greater(ship.MaxSpeed, 0f);
            UseFixedCurveProfile(kit);

            ship.Shake.Raised += (_, _, _) => raises++;
            ship.Shake.Scaled += (_, strength) =>
            {
                scales++;
                scaledStrength = strength;
            };
            ship.Shake.Released += _ => releases++;
        }

        [TearDown]
        public override void TearDown()
        {
            if (kitHost) Object.DestroyImmediate(kitHost);
            if (ship) Object.DestroyImmediate(ship.gameObject);
            if (profileCopy) Object.DestroyImmediate(profileCopy);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Activation_AtZeroSpeed_RaisesOnce_AndTheNextFrameScalesToZero()
        {
            Activate(kit);
            yield return null;
            Assert.AreEqual(1, raises);
            Assert.AreEqual(1, scales);
            Assert.AreEqual(0f, scaledStrength, 1e-4f);
        }

        [UnityTest]
        public IEnumerator AtOneAndAHalfTopSpeed_ScalesToOne()
        {
            motion = new Kinematics(Vector2.zero, new Vector2(0f, ship.MaxSpeed * 1.5f), 0f, 0f, 0f);
            Activate(kit);
            yield return null;
            Assert.AreEqual(1f, scaledStrength, 1e-4f);
        }

        [UnityTest]
        public IEnumerator AfterTheWindow_HoldsWhileFast_AndReleasesWhenStopped()
        {
            motion = new Kinematics(Vector2.zero, new Vector2(0f, ship.MaxSpeed * 1.5f), 0f, 0f, 0f);
            Activate(kit);
            yield return null;

            kit.Step(Still, 0f, kit.Window, out _);
            Assert.IsFalse(kit.Active);
            yield return null;
            Assert.AreEqual(0, releases);

            motion = Still;
            yield return null;
            Assert.AreEqual(1, releases);
        }

        private Overdrive SpawnKit(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitPrefabPath);
            Assert.IsNotNull(prefab, "the overdrive kit prefab loads");
            kitHost = parent ? Object.Instantiate(prefab, parent) : Object.Instantiate(prefab);
            var spawned = kitHost.GetComponent<Overdrive>();
            Assert.IsNotNull(spawned, "the kit prefab carries Overdrive");
            return spawned;
        }

        private void UseFixedCurveProfile(Keystone keystone)
        {
            var shake = keystone.GetComponent<KeystoneSpeedShake>();
            Assert.IsNotNull(shake, "the overdrive kit prefab carries its speed shake");
            using var serialized = new SerializedObject(shake);
            var property = serialized.FindProperty("profile");
            var realProfile = (CameraShakeProfile)property.objectReferenceValue;
            Assert.IsNotNull(realProfile, "the kit's speed shake references a profile");

            // Assertions use a fixed curve so tuning the real profile cannot break them.
            profileCopy = Object.Instantiate(realProfile);
            profileCopy.bySpeed = new AnimationCurve(new Keyframe(1f, 0f), new Keyframe(1.5f, 1f));
            property.objectReferenceValue = profileCopy;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Activate(Keystone keystone)
        {
            keystone.Step(Still, 1f, Time.fixedDeltaTime, out var activated);
            Assert.IsTrue(activated);
        }
    }
}
