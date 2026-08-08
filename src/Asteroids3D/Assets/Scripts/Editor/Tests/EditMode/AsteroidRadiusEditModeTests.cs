#if UNITY_EDITOR
using Asteroids;
using Asteroids.Spawning;
using AsteroidTools;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The two baked geometry statistics and the scalar the runtime derives from one of
    /// them. Mean-vertex radius now serves only the K=1 lobe bake; the runtime's
    /// broadphase scalar is the volume-equivalent radius, so the two are tested apart.
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
            Assert.That(AsteroidLobeBaker.MeanVertexRadius(mesh), Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void MeanVertexRadius_UnitCube_IsCornerDistance_AndTighterThanAabbMaxAxisWouldOverstate()
        {
            // The unit cube's 24 vertices all sit at the corners: |(±.5, ±.5, ±.5)| = √0.75.
            // The old max-AABB-axis formula would report 0.5 here — for a cube the corner
            // distance is LARGER, which is exactly why this is a statistic of the real
            // vertices, not the bounding box: it tracks actual geometry in both directions.
            var mesh = PrimitiveMesh(PrimitiveType.Cube);
            Assert.That(AsteroidLobeBaker.MeanVertexRadius(mesh),
                Is.EqualTo(Mathf.Sqrt(0.75f)).Within(0.01f));
        }

        [Test]
        public void RadiusFromVolume_UnitSphereVolume_IsUnitRadius()
        {
            Assert.That(AsteroidGeometry.RadiusFromVolume(4f / 3f * Mathf.PI),
                Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void RadiusFromVolume_NonPositive_IsZero()
        {
            // Only a failed bake produces this, and the build gate refuses to ship one —
            // so the contract is a recognisable zero, not a guessed fallback.
            Assert.That(AsteroidGeometry.RadiusFromVolume(0f), Is.EqualTo(0f));
            Assert.That(AsteroidGeometry.RadiusFromVolume(-1f), Is.EqualTo(0f));
        }

        [Test]
        public void MeshVolume_UnitSphere_MatchesAnalyticVolume()
        {
            // Unity's sphere primitive is a faceted approximation, so it under-reports the
            // true 4/3·π·0.5³ — a few percent of slack, not a tight equality.
            var mesh = PrimitiveMesh(PrimitiveType.Sphere);
            Assert.That(AsteroidMeshVolume.TryCompute(mesh, out var volume, out _), Is.True);
            Assert.That(volume, Is.EqualTo(4f / 3f * Mathf.PI * 0.125f).Within(0.02f));
        }

        [Test]
        public void MeshVolume_UnitCube_IsOne()
        {
            var mesh = PrimitiveMesh(PrimitiveType.Cube);
            Assert.That(AsteroidMeshVolume.TryCompute(mesh, out var volume, out _), Is.True);
            Assert.That(volume, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void MeshVolume_CentroidOfCenteredPrimitive_IsOrigin()
        {
            var mesh = PrimitiveMesh(PrimitiveType.Cube);
            Assert.That(AsteroidMeshVolume.TryCompute(mesh, out _, out var centroid), Is.True);
            Assert.That(centroid.magnitude, Is.LessThan(1e-4f));
        }

        [Test]
        public void MeshVolume_EmptyMesh_IsDegenerate()
        {
            var mesh = new Mesh();
            Assert.That(AsteroidMeshVolume.TryCompute(mesh, out _, out _), Is.False);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void VolumePostprocessor_IsDiscoverableByUnity()
        {
            // Unity only finds OnPostprocessAllAssets on AssetPostprocessor subclasses. Get
            // this wrong and the auto-bake silently never runs — no error, no import, just
            // staleness waiting for the build gate. Cheap pin on an expensive silent failure.
            Assert.That(typeof(AsteroidVolumePostprocessor).IsSubclassOf(typeof(UnityEditor.AssetPostprocessor)),
                Is.True);
        }

        [Test]
        public void ShippedSettings_PassTheBuildGate()
        {
            // The gate's real job, run as a test so a stale bake fails the merge gate rather
            // than waiting for someone to make a player build.
            Assert.DoesNotThrow(AsteroidGeometryBuildGate.Validate);
        }
    }
}
#endif
