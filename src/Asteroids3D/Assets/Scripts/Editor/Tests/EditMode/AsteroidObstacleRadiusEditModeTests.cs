#if UNITY_EDITOR
using Asteroids.Spawning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Utils;

namespace Tests.EditMode
{
    /// <summary>
    /// The authoritative asteroid obstacle radius (ColliderPlanarRadius over the actual
    /// collision mesh, as cached by AsteroidController.Initialize) must be a TIGHT in-plane
    /// circle: it contains every plane-projected collider vertex under any rotation the
    /// tumbling asteroid can take, and it is within a few percent of the tightest such
    /// circle at the worst-case rotation. One case per asteroid mesh variant in the
    /// production spawn settings.
    /// </summary>
    [TestFixture]
    [Category("Scanning")]
    public class AsteroidObstacleRadiusEditModeTests
    {
        private const string SpawnSettingsPath = "Assets/Settings/Asteroids/SpawnSettings.asset";
        private const int RandomRotations = 32;
        private const float ContainmentSlack = 1e-3f;
        private const float TightnessTolerance = 1.02f; // "within a few percent"

        private static AsteroidSpawnSettings Settings =>
            AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(SpawnSettingsPath);

        private static int VariantCount()
        {
            var settings = Settings;
            Assert.IsNotNull(settings, $"Missing spawn settings at {SpawnSettingsPath}");
            return settings.meshInfos.Length;
        }

        private static Mesh CollisionMesh(int variant)
        {
            var info = Settings.meshInfos[variant];
            var mesh = info.colliderMesh ? info.colliderMesh : info.mesh;
            Assert.IsNotNull(mesh, $"Variant {variant} has no collision mesh");
            Assert.IsTrue(mesh.isReadable, $"{mesh.name}: collision mesh must be CPU-readable");
            return mesh;
        }

        private static float MaxPlanarDistance(Vector3[] verts, Quaternion rot, float scale)
        {
            var maxSq = 0f;
            for (var i = 0; i < verts.Length; i++)
            {
                var v = rot * (verts[i] * scale);
                var planarSq = v.x * v.x + v.y * v.y; // plane = XY of the rotated frame
                if (planarSq > maxSq) maxSq = planarSq;
            }
            return Mathf.Sqrt(maxSq);
        }

        [Test]
        public void EveryVariant_RadiusContainsAllPlaneProjectedColliderPoints(
            [NUnit.Framework.Range(0, 9)] int variant)
        {
            Assume.That(variant, Is.LessThan(VariantCount()));
            var mesh = CollisionMesh(variant);
            var verts = mesh.vertices;
            var radius = ColliderPlanarRadius.MaxVertexNorm(mesh);

            // Random spawn-realistic rotations and scales (field scale ≈ 0.79–1.36).
            var rng = new System.Random(1234 + variant);
            foreach (var scale in new[] { 0.79f, 1f, 1.36f })
            {
                for (var r = 0; r < RandomRotations; r++)
                {
                    var rot = Quaternion.Euler(
                        (float)rng.NextDouble() * 360f,
                        (float)rng.NextDouble() * 360f,
                        (float)rng.NextDouble() * 360f);
                    var maxPlanar = MaxPlanarDistance(verts, rot, scale);
                    Assert.LessOrEqual(maxPlanar, radius * scale + ContainmentSlack,
                        $"{mesh.name}: plane-projected collider point escapes the obstacle circle " +
                        $"(scale {scale}, rotation {rot.eulerAngles})");
                }
            }
        }

        [Test]
        public void EveryVariant_RadiusIsWithinFewPercentOfTightestCircle(
            [NUnit.Framework.Range(0, 9)] int variant)
        {
            Assume.That(variant, Is.LessThan(VariantCount()));
            var mesh = CollisionMesh(variant);
            var verts = mesh.vertices;
            var radius = ColliderPlanarRadius.MaxVertexNorm(mesh);

            // Worst-case rotation: the farthest vertex rotated into the plane. There the
            // tightest circle (about the pivot) containing the projected collider has radius
            // exactly |v_max| — the cached radius must not exceed it by more than a few %.
            var farthest = Vector3.zero;
            var maxSq = 0f;
            for (var i = 0; i < verts.Length; i++)
            {
                if (verts[i].sqrMagnitude <= maxSq) continue;
                maxSq = verts[i].sqrMagnitude;
                farthest = verts[i];
            }

            var intoPlane = Quaternion.FromToRotation(farthest, Vector3.right);
            var tightest = MaxPlanarDistance(verts, intoPlane, 1f);
            Assert.Greater(tightest, 0f, $"{mesh.name}: degenerate collision mesh");
            Assert.LessOrEqual(radius, tightest * TightnessTolerance,
                $"{mesh.name}: cached radius {radius:F3} is not tight against the worst-case " +
                $"in-plane circle {tightest:F3}");
        }
    }
}
#endif
