#if UNITY_EDITOR
using System.Collections;
using AI.Scanning;
using Asteroids;
using Asteroids.Fields;
using NUnit.Framework;
using Substrate;
using Substrate.Sectors.Elements.Placement;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Sectors
{
    [Category("Sectors")]
    public class AsteroidPlacementClearancePlayModeTests : PlayModeWorldFixture
    {
        private const string FieldPrefabPath = "Assets/Prefabs/Asteroid/AsteroidController.prefab";

        private GameObject fieldGo;
        private GameObject anchorGo;

        [TearDown]
        public override void TearDown()
        {
            try
            {
                if (fieldGo) Object.DestroyImmediate(fieldGo);
                if (anchorGo) Object.DestroyImmediate(anchorGo);
            }
            finally
            {
                base.TearDown();
            }
        }

        [UnityTest]
        public IEnumerator FarRock_WithItsHullOff_RefusesAPointInsideItsRadius()
        {
            var field = SpawnAnchoredField();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;

            var buffer = new DetectedObstacle[2048];
            var count = field.QueryObstacles(Vector2.zero, 90f, buffer);
            AsteroidController rock = null;
            var rockPlane = Vector2.zero;
            for (var i = 0; i < count; i++)
            {
                var candidate = buffer[i].source;
                if (!candidate) continue;
                var plane = GamePlane.WorldPointToPlane(candidate.transform.position);
                if (plane.magnitude <= 80f) continue;
                rock = candidate;
                rockPlane = plane;
                break;
            }
            Assert.That(rock, Is.Not.Null, "a live rock sits beyond the hull LOD");

            Assert.That(rock.GetComponent<MeshCollider>().enabled, Is.False);
            Assert.That(rock.GetComponentsInChildren<SphereCollider>(true), Is.Empty);
            Assert.That(Physics.CheckSphere(rock.transform.position, 0.1f, LayerIds.Mask(LayerIds.Asteroid)), Is.False);
            Assert.That(ClearPoint.IsClear(field, rockPlane + Vector2.right * (0.5f * rock.Radius), 0.1f), Is.False);
            Assert.That(ClearPoint.IsClear(field, new Vector2(5000f, 5000f), 6f), Is.True);
            Assert.That(ClearPoint.IsClear(null, rockPlane, 6f), Is.True);
        }

        [UnityTest]
        public IEnumerator DriftedRock_IsFoundFarFromItsHomeChunk()
        {
            var field = SpawnAnchoredField();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var buffer = new DetectedObstacle[2048];
            var count = field.QueryObstacles(Vector2.zero, 90f, buffer);
            Assert.That(count, Is.GreaterThan(0));
            var rock = buffer[0].source;
            Assert.That(rock, Is.Not.Null);

            var p = new Vector2(500f, 0f);
            Assert.That(ClearPoint.IsClear(field, p, 6f), Is.True);
            var world = GamePlane.PlanePointToWorld(p);
            rock.Rb.position = world;
            rock.transform.position = world;
            Physics.SyncTransforms();
            Assert.That(ClearPoint.IsClear(field, p, 6f), Is.False);
        }

        private UpdatingAsteroidField SpawnAnchoredField()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FieldPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Field prefab not found at {FieldPrefabPath}");
            fieldGo = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            var field = fieldGo.GetComponent<AsteroidField>() as UpdatingAsteroidField;
            Assert.That(field, Is.Not.Null, "Field prefab is not an UpdatingAsteroidField");
            anchorGo = new GameObject("FieldAnchor");
            anchorGo.transform.position = Vector3.zero;
            field.SetAnchor(anchorGo.transform);
            return field;
        }
    }
}
#endif
