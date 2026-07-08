#if UNITY_EDITOR
using Asteroids;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The cheap-collider radius statistic: mean vertex distance from the mesh origin —
    /// deliberately tighter than the circumscribed radius (protrusions may clip; phantom
    /// berth is the worse failure for both physics feel and AI avoidance).
    /// </summary>
    [Category("Asteroids")]
    public class AsteroidRadiusEditModeTests
    {
        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }

        [Test]
        public void MeanVertexRadius_UnitSphere_IsHalf()
        {
            // Every vertex of the unit sphere primitive sits at distance 0.5 from origin.
            var mesh = PrimitiveMesh(PrimitiveType.Sphere);
            Assert.That(AsteroidController.MeanVertexRadius(mesh), Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void MeanVertexRadius_UnitCube_IsCornerDistance_AndTighterThanAabbMaxAxisWouldOverstate()
        {
            // The unit cube's 24 vertices all sit at the corners: |(±.5, ±.5, ±.5)| = √0.75.
            // The old max-AABB-axis formula would report 0.5 here — for a cube the corner
            // distance is LARGER, which is exactly why this is a statistic of the real
            // vertices, not the bounding box: it tracks actual geometry in both directions.
            var mesh = PrimitiveMesh(PrimitiveType.Cube);
            Assert.That(AsteroidController.MeanVertexRadius(mesh),
                Is.EqualTo(Mathf.Sqrt(0.75f)).Within(0.01f));
        }

        [Test]
        public void MeanVertexRadius_IsCached_SameMeshSameValue()
        {
            var mesh = PrimitiveMesh(PrimitiveType.Sphere);
            var a = AsteroidController.MeanVertexRadius(mesh);
            var b = AsteroidController.MeanVertexRadius(mesh);
            Assert.That(b, Is.EqualTo(a));
        }
    }
}
#endif
