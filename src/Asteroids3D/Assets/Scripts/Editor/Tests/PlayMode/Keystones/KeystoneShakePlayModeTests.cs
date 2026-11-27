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
    public class KeystoneShakePlayModeTests : PlayModeWorldFixture
    {
        private const string KitPrefabPath = "Assets/Prefabs/Keystones/Junker_Dash.prefab";

        private static readonly Kinematics Still = new(Vector2.zero, Vector2.zero, 0f, 0f, 0f);

        private GameObject kitHost;
        private Ship ship;
        private CameraShakeProfile profile;

        [TearDown]
        public override void TearDown()
        {
            if (kitHost) Object.DestroyImmediate(kitHost);
            if (ship) Object.DestroyImmediate(ship.gameObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Activation_RaisesTheProfile_AndReleasesTheSameFrame()
        {
            ship = ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero);
            var kit = SpawnKit(ship.transform);
            kit.Initialize(new KeystoneHost(ship, ship.Rigidbody, 1f, ship.Lock));
            ship.Movement.Initialize(ship.Stats, () => Still, kit, null, ship.Status);
            ReadProfile(kit);

            var raises = 0;
            CameraShakeProfile raised = null;
            var direction = Vector2.zero;
            var releases = 0;
            ship.Shake.Raised += (_, shake, aimed) =>
            {
                raises++;
                raised = shake;
                direction = aimed;
            };
            ship.Shake.Released += _ => releases++;

            Activate(kit);
            yield return null;
            Assert.AreEqual(1, raises);
            Assert.AreSame(profile, raised);
            Assert.That(Vector2.Distance(direction, ship.Kinematics.Forward), Is.LessThan(1e-4f));
            Assert.AreEqual(1, releases);
        }

        [UnityTest]
        public IEnumerator Activation_WithoutAShip_RaisesNothing()
        {
            var kit = SpawnKit(null);
            ReadProfile(kit);
            Activate(kit);
            yield return null;
            Assert.AreEqual(1, kit.Activations);
            Assert.IsNull(kit.GetComponentInParent<Ship>());
        }

        private Dash SpawnKit(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitPrefabPath);
            Assert.IsNotNull(prefab, "the dash kit prefab loads");
            kitHost = parent ? Object.Instantiate(prefab, parent) : Object.Instantiate(prefab);
            var kit = kitHost.GetComponent<Dash>();
            Assert.IsNotNull(kit, "the kit prefab carries Dash");
            return kit;
        }

        private void ReadProfile(Keystone kit)
        {
            var shake = kit.GetComponent<KeystoneShake>();
            Assert.IsNotNull(shake, "the dash kit prefab carries its shake part");
            using var serialized = new SerializedObject(shake);
            profile = (CameraShakeProfile)serialized.FindProperty("profile").objectReferenceValue;
            Assert.IsNotNull(profile, "the kit's shake part references a profile");
        }

        private static void Activate(Keystone kit)
        {
            kit.Step(Still, 1f, Time.fixedDeltaTime, out var activated);
            Assert.IsTrue(activated);
        }
    }
}
