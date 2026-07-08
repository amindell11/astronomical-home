#if UNITY_EDITOR
using Asteroids;
using Asteroids.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The multi-sphere ("lobe") decomposition baker: deterministic covering spheres
    /// along a mesh's principal axis. K=1 must reproduce the single mean-vertex circle;
    /// elongated meshes split into 2..3 lobes.
    /// </summary>
    [Category("Asteroids")]
    public class AsteroidLobeBakerEditModeTests
    {
        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh; // built-in shared asset (not destroyed)
            Object.DestroyImmediate(go);
            return mesh;
        }

        [Test]
        public void Bake_IsDeterministic_IdenticalLobesAcrossRuns()
        {
            // Capsule exercises the K-means (K>=2) path — the interesting one for determinism.
            var mesh = PrimitiveMesh(PrimitiveType.Capsule);
            var a = AsteroidLobeBaker.Bake(mesh, out float aspectA);
            var b = AsteroidLobeBaker.Bake(mesh, out float aspectB);

            Assert.That(aspectB, Is.EqualTo(aspectA));
            Assert.That(b.Length, Is.EqualTo(a.Length));
            for (int i = 0; i < a.Length; i++)
            {
                Assert.That(b[i].center, Is.EqualTo(a[i].center));
                Assert.That(b[i].radius, Is.EqualTo(a[i].radius));
            }
        }

        [Test]
        public void Bake_NearSphere_IsSingleLobe_MatchingMeanVertexRadius()
        {
            var mesh = PrimitiveMesh(PrimitiveType.Sphere); // isotropic → K=1
            var lobes = AsteroidLobeBaker.Bake(mesh, out float aspect);

            Assert.That(aspect, Is.LessThan(1.3f), "isotropic sphere should classify K=1");
            Assert.That(lobes.Length, Is.EqualTo(1));
            Assert.That(lobes[0].center, Is.EqualTo(Vector3.zero));
            Assert.That(lobes[0].radius,
                Is.EqualTo(AsteroidController.MeanVertexRadius(mesh)).Within(1e-5f));
        }

        [Test]
        public void Bake_Rod_IsTwoLobes_SeparatedAlongLongAxis_EachTighterThanSingleCircle()
        {
            // Unity's capsule is elongated along Y (height 2, radius 0.5 → extents ~2,1,1),
            // finely tessellated so the area-weighted PCA is clean.
            var mesh = PrimitiveMesh(PrimitiveType.Capsule);
            float singleCircle = AsteroidController.MeanVertexRadius(mesh);
            var lobes = AsteroidLobeBaker.Bake(mesh, out float aspect);

            Assert.That(aspect, Is.GreaterThanOrEqualTo(1.3f).And.LessThan(2.5f),
                "an elongated rod should classify K=2");
            Assert.That(lobes.Length, Is.EqualTo(2));

            // Centers well separated, and the separation is dominated by a single (long) axis.
            Vector3 delta = lobes[0].center - lobes[1].center;
            float sep = delta.magnitude;
            Assert.That(sep, Is.GreaterThan(0.7f), "lobe centers should be separated along the long axis");
            float maxComp = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
            Assert.That(maxComp, Is.GreaterThan(0.9f * sep),
                "separation should be dominated by one principal axis");

            foreach (var lobe in lobes)
                Assert.That(lobe.radius, Is.LessThan(singleCircle),
                    "each split lobe should be tighter than the single mean-vertex circle");
        }

        [Test]
        public void ClassifyK_BoundariesAt_1p3_And_2p5()
        {
            Assert.That(AsteroidLobeBaker.ClassifyK(1.29f), Is.EqualTo(1));
            Assert.That(AsteroidLobeBaker.ClassifyK(1.31f), Is.EqualTo(2));
            Assert.That(AsteroidLobeBaker.ClassifyK(2.49f), Is.EqualTo(2));
            Assert.That(AsteroidLobeBaker.ClassifyK(2.51f), Is.EqualTo(3));
        }
    }
}
#endif
