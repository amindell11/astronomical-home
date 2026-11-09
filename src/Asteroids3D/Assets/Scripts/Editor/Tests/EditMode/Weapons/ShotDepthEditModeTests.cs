using System.Collections.Generic;
using System.Linq;
using Combat.Projectiles;
using Combat.Projectiles.Visual;
using Combat.Weapons;
using NUnit.Framework;
using Ships.Loadout;
using Substrate;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Weapons
{
    /// <summary>
    /// Shots draw under hulls: every hull the camera draws stays above <see cref="ShotDepth"/>, every
    /// shot visual sits below it with its collider still on the plane, and every shot material draws
    /// before the ship ink so the ink outline crosses over a shot.
    /// </summary>
    [TestFixture]
    [Category("Weapons")]
    public sealed class ShotDepthEditModeTests
    {
        private const string MainCameraPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string RailgunPath = "Assets/Prefabs/Weapons/Railgun.prefab";
        private const string InkShader = "Astronomical/Drawn/Contour";
        private const float Tolerance = 1e-4f;

        // Predates the drawn hulls and reaches 0.97 below the plane, so shots still draw over it.
        private const string CargoHull = "Junker_1";

        private static ItemCatalog Catalog => AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);

        public static IEnumerable<TestCaseData> Hulls() =>
            Catalog.Chassis.Where(chassis => chassis.name != CargoHull)
                .Select(chassis => new TestCaseData(AssetDatabase.GetAssetPath(chassis)).SetName($"{{m}}({chassis.name})"));

        public static IEnumerable<TestCaseData> Shots() =>
            ShotPaths().Select(path => new TestCaseData(path).SetName($"{{m}}({System.IO.Path.GetFileNameWithoutExtension(path)})"));

        [TestCaseSource(nameof(Hulls))]
        public void Hull_StaysAboveShotDepth(string path)
        {
            var mask = AssetDatabase.LoadAssetAtPath<Camera>(MainCameraPath).cullingMask;
            WithPlaced(path, root =>
            {
                var hull = root.GetComponentsInChildren<Renderer>()
                    .Where(r => r.enabled && (mask & (1 << r.gameObject.layer)) != 0
                                && r.sharedMaterials.Any(m => m && m.renderQueue <= (int)UnityEngine.Rendering.RenderQueue.GeometryLast))
                    .ToArray();
                Assert.That(hull, Is.Not.Empty, $"{path} has no opaque renderer the game camera draws");
                foreach (var renderer in hull)
                    Assert.That(Depth(renderer.bounds.max), Is.LessThan(ShotDepth.BelowPlane),
                        $"{path}: '{renderer.name}' reaches below the shot depth, so shots would draw over it");
            });
        }

        [TestCaseSource(nameof(Shots))]
        public void Shot_DrawsBelowShotDepth_AndCollidesOnThePlane(string path)
        {
            WithPlaced(path, root =>
            {
                var visuals = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                Assert.That(visuals, Is.Not.Empty, $"{path} draws nothing");
                foreach (var renderer in visuals)
                {
                    // A view-facing particle is flat to the top-down camera, so its emitter's depth is its depth.
                    var particles = renderer as ParticleSystemRenderer;
                    var nearest = particles ? renderer.transform.position : renderer.bounds.min;
                    Assert.That(Depth(nearest), Is.GreaterThanOrEqualTo(ShotDepth.BelowPlane - Tolerance),
                        $"{path}: '{renderer.name}' draws above the shot depth, so it would show over a hull");
                }

                foreach (var collider in root.GetComponentsInChildren<Collider>())
                    Assert.That(Depth(Centre(collider)), Is.EqualTo(0f).Within(Tolerance),
                        $"{path}: collider '{collider.name}' left the plane");
            });
        }

        [Test]
        public void ShotMaterials_DrawBeforeEveryShipInk()
        {
            var inkQueue = Catalog.Chassis
                .SelectMany(chassis => chassis.GetComponentsInChildren<Renderer>(true))
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m && m.shader.name == InkShader)
                .Min(m => m.renderQueue);

            var railgun = AssetDatabase.LoadAssetAtPath<GameObject>(RailgunPath).GetComponent<LineRenderer>();
            var shotMaterials = ShotPaths()
                .SelectMany(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Renderer>(true))
                .Append(railgun)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m)
                .Distinct();
            foreach (var material in shotMaterials)
                Assert.That(material.renderQueue, Is.LessThan(inkQueue),
                    $"'{material.name}' draws after the ship ink (queue {inkQueue}), so a shot would cross over a hull's outline");
        }

        private static IEnumerable<string> ShotPaths() =>
            Catalog.Weapons
                .Select(weapon => new SerializedObject(weapon).FindProperty(nameof(WeaponBase<ProjectileBase>.projectilePrefab)))
                .Where(property => property != null && property.objectReferenceValue)
                .Select(property => AssetDatabase.GetAssetPath(property.objectReferenceValue))
                .Distinct();

        private static float Depth(Vector3 world) => Vector3.Dot(world - GamePlane.Origin, GamePlane.Normal);

        private static Vector3 Centre(Collider collider)
        {
            var sphere = collider as SphereCollider;
            var capsule = collider as CapsuleCollider;
            var box = collider as BoxCollider;
            var local = sphere ? sphere.center : capsule ? capsule.center : box ? box.center : Vector3.zero;
            return collider.transform.TransformPoint(local);
        }

        // Placed as a spawn places it: the root on the plane in the plane pose.
        private static void WithPlaced(string path, System.Action<GameObject> check)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.transform.SetPositionAndRotation(GamePlane.Origin, GamePlane.Rotation);
                check(root);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
