#if UNITY_EDITOR
using Asteroids;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Asteroids")]
    public class AsteroidColliderEditModeTests
    {
        [TestCase("Assets/Prefabs/Asteroid/Asteroid3D.prefab")]
        [TestCase("Assets/Visuals/Vfx/LayeredExplosion/Prefabs/FragmentingDrawnAsteroid.prefab")]
        public void AsteroidPrefab_CarriesNoSphereCollider(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.GetComponent<AsteroidController>(), Is.Not.Null, path);
            Assert.That(prefab.GetComponentsInChildren<SphereCollider>(true), Is.Empty, path);
        }

        [Test]
        public void AddedAsteroidController_BringsNoSphereCollider()
        {
            var go = new GameObject("asteroid");
            try
            {
                go.AddComponent<Rigidbody>();
                go.AddComponent<AsteroidController>();
                Assert.That(go.GetComponent<SphereCollider>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
