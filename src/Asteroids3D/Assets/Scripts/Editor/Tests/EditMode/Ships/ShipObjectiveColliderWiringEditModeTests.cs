using System.Linq;
using NUnit.Framework;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("Ships")]
    public class ShipObjectiveColliderWiringEditModeTests
    {
        private const string Ship2PrefabPath = "Assets/Prefabs/Ships/Ship_2.prefab";

        [Test]
        public void Ship2Prefab_HasObjectiveCompatibleCollider()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Ship>(Ship2PrefabPath);
            Assert.IsNotNull(prefab, "Ship_2 prefab failed to load");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            Assert.IsNotNull(instance, "Ship_2 prefab failed to instantiate");

            try
            {
                AssertObjectiveCollider(instance.GetComponent<Ship>());
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void AssertObjectiveCollider(Ship ship)
        {
            var body = ship.GetComponent<Rigidbody>();
            Assert.IsNotNull(body, "Ship_2 must carry its player Rigidbody");

            var colliders = ship.GetComponentsInChildren<Collider>(true);
            var compatible = colliders
                .Where(c => c.enabled && !c.isTrigger && c.attachedRigidbody == body)
                .Where(c => !Physics.GetIgnoreLayerCollision(0, c.gameObject.layer))
                .ToArray();

            Assert.IsTrue(compatible.Any(HasGeometry),
                "Ship_2 needs loaded collider geometry that can enter Default-layer objective triggers. " +
                string.Join("; ", colliders.Select(c =>
                    $"{c.name}: layer={c.gameObject.layer}, body={c.attachedRigidbody == body}, " +
                    $"enabled={c.enabled}, trigger={c.isTrigger}, geometry={HasGeometry(c)}")));
        }

        private static bool HasGeometry(Collider collider) =>
            collider is not MeshCollider mesh || mesh.sharedMesh != null;
    }
}
