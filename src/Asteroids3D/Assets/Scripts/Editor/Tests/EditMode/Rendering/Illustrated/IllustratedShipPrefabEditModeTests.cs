using System.Linq;
using NUnit.Framework;
using Ships.Presentation;
using Ships.Visuals;
using Substrate;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rendering.Illustrated
{
    [Category("Ships")]
    public sealed class IllustratedShipPrefabEditModeTests
    {
        private const string SurfaceShader = "Astronomical/Comparison/Drawn Surface";
        private const string ContourShader = "Astronomical/Comparison/Drawn Contour";

        [TestCase(1, "Vanguard")]
        [TestCase(2, "Crimson")]
        public void IllustratedShip_HasOneRigWithSavedGameplaySurfacesAndContours(int number, string hullName)
        {
            var ship = LoadShip(number);
            var rigs = ship.GetComponentsInChildren<ShipVisualRig>(true);
            Assert.That(rigs, Has.Length.EqualTo(1), "A variant must not inherit a second visual rig.");
            var hull = rigs[0].GetComponentsInChildren<Transform>(true).Single(t => t.name == hullName);
            var surfaces = hull.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => UsesShader(r, SurfaceShader)).ToArray();
            var contours = hull.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => UsesShader(r, ContourShader)).ToArray();
            Assert.That(surfaces, Is.Not.Empty, "The saved hull must carry its painted geometry.");
            Assert.That(contours, Is.Not.Empty, "Outlines must exist before a ship is spawned.");
            foreach (var renderer in surfaces.Concat(contours))
            {
                Assert.That(renderer.enabled && IsActiveInPrefab(renderer.transform), Is.True, renderer.name);
                Assert.That(renderer.gameObject.layer, Is.EqualTo(LayerIds.Ship), renderer.name);
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null, renderer.name);
                Assert.That(AssetDatabase.Contains(mesh), Is.True, "Ship spawning must not generate drawing meshes.");
                Assert.That(renderer.sharedMaterials.All(AssetDatabase.Contains), Is.True);
            }
            var feedback = rigs[0].GetComponentsInChildren<HullVisuals>(true);
            Assert.That(feedback, Has.Length.EqualTo(1));
            Assert.That(feedback[0].enabled, Is.False, "Multi-surface damage feedback is deferred.");
        }

        [Test]
        public void Ship3_PreservesItsLegacyRigAndDamageFeedback()
        {
            var ship = LoadShip(3);
            var rigs = ship.GetComponentsInChildren<ShipVisualRig>(true);
            Assert.That(rigs, Has.Length.EqualTo(1));
            Assert.That(rigs[0].name, Is.EqualTo("Ship_3_VisualRig"));
            var feedback = rigs[0].GetComponentsInChildren<HullVisuals>(true);
            Assert.That(feedback, Has.Length.EqualTo(1));
            Assert.That(feedback[0].enabled, Is.True);
            Assert.That(ship.GetComponentsInChildren<Transform>(true)
                .Any(t => t.name == "Vanguard" || t.name == "Crimson"), Is.False);
            Assert.That(rigs[0].GetComponentsInChildren<MeshRenderer>(true)
                .Any(r => r.enabled && !UsesShader(r, ContourShader)), Is.True);
        }

        [Test]
        public void Crimson_HullHasOneMeshAndRendererWithDistinctPaintAndContourVertices()
        {
            var hull = LoadShip(2).GetComponentsInChildren<Transform>(true).Single(t => t.name == "Crimson");
            var renderers = hull.GetComponentsInChildren<MeshRenderer>(true);
            var filters = hull.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(renderers, Has.Length.EqualTo(1));
            Assert.That(filters, Has.Length.EqualTo(1));
            Assert.That(renderers[0].gameObject, Is.EqualTo(hull.gameObject));
            var mesh = filters[0].sharedMesh;
            Assert.That(mesh.subMeshCount, Is.EqualTo(2));
            Assert.That(renderers[0].sharedMaterials.Select(m => m.shader.name),
                Is.EqualTo(new[] { SurfaceShader, ContourShader }));
            Assert.That(mesh.GetIndexCount(0), Is.GreaterThan(0));
            Assert.That(mesh.GetIndexCount(1), Is.EqualTo(mesh.GetIndexCount(0)));
            Assert.That(mesh.GetIndices(0).Intersect(mesh.GetIndices(1)), Is.Empty,
                "Painted and contour normals require separate vertex ranges within the same mesh.");
        }

        [Test]
        public void Crimson_ProwFacesForwardAndCanopyFacesTheGameplayCamera()
        {
            var ship = LoadShip(2);
            var visual = ship.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Crimson");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visuals/Ships/Crimson/Crimson.fbx");
            var parts = source.GetComponentsInChildren<MeshRenderer>(true);
            Vector3 Center(string name)
            {
                var part = parts.Single(r => r.name == name);
                var point = source.transform.InverseTransformPoint(
                    part.transform.TransformPoint(part.GetComponent<MeshFilter>().sharedMesh.bounds.center));
                return ship.transform.InverseTransformPoint(visual.TransformPoint(point));
            }
            var prow = Center("Upper prow tip");
            var nozzle = Center("Engine nozzle");
            var canopy = Center("Canopy");
            var hull = Center("Central hull");
            Assert.That(Vector3.Dot((prow - nozzle).normalized, Vector3.up), Is.GreaterThan(.99f));
            Assert.That(canopy.z, Is.LessThan(hull.z), "The camera sees the canopy, not the underside.");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ShipColliderBounds_EncloseThePaintedHullInShipCoordinates(int number)
        {
            var ship = LoadShip(number);
            var colliders = ship.GetComponentsInChildren<MeshCollider>(true)
                .Where(c => c.enabled && !c.isTrigger && c.sharedMesh).ToArray();
            Assert.That(colliders, Is.Not.Empty);
            var colliderBounds = BoundsInShip(ship.transform, colliders[0].transform, colliders[0].sharedMesh.bounds);
            foreach (var collider in colliders.Skip(1))
                colliderBounds.Encapsulate(BoundsInShip(ship.transform, collider.transform, collider.sharedMesh.bounds));
            colliderBounds.Expand(.002f);
            var surfaces = ship.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => UsesShader(r, SurfaceShader)).ToArray();
            Assert.That(surfaces, Is.Not.Empty);
            foreach (var surface in surfaces)
            {
                var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
                using var meshData = MeshUtility.AcquireReadOnlyMeshData(mesh);
                using var vertices = new NativeArray<Vector3>(meshData[0].vertexCount, Allocator.Temp);
                meshData[0].GetVertices(vertices);
                var matrix = ship.transform.worldToLocalMatrix * surface.transform.localToWorldMatrix;
                foreach (var vertex in vertices)
                {
                    var point = matrix.MultiplyPoint3x4(vertex);
                    Assert.That(colliderBounds.Contains(point), Is.True,
                        $"Ship_{number} collider excludes {surface.name} vertex {point}: collider {colliderBounds}.");
                }
            }
        }

        private static bool IsActiveInPrefab(Transform transform)
        {
            for (var current = transform; current; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }

        private static GameObject LoadShip(int number)
        {
            var ship = AssetDatabase.LoadMainAssetAtPath($"Assets/Prefabs/Ships/Ship_{number}.prefab") as GameObject;
            Assert.That(ship, Is.Not.Null);
            return ship;
        }

        private static bool UsesShader(MeshRenderer renderer, string shader) =>
            renderer.sharedMaterials.Any(m => m && m.shader && m.shader.name == shader);

        private static Bounds BoundsInShip(Transform ship, Transform part, Bounds local)
        {
            var matrix = ship.worldToLocalMatrix * part.localToWorldMatrix;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (var corner = 0; corner < 8; corner++)
            {
                var point = local.center + Vector3.Scale(local.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                bounds.Encapsulate(matrix.MultiplyPoint3x4(point));
            }
            return bounds;
        }
    }
}
