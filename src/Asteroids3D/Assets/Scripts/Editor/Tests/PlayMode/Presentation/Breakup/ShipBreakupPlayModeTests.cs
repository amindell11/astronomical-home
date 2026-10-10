using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Damage;
using NUnit.Framework;
using Ships;
using Ships.Registry;
using Ships.Visuals.Breakup;
using Substrate.Services;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation.Breakup
{
    [Category("Ships")]
    public sealed class ShipBreakupPlayModeTests : PlayModeWorldFixture
    {
        private GameObject host;
        private UnitService units;
        private float savedMaxDelta;
        private static readonly int Visibility = Shader.PropertyToID("_DebrisVisibility");

        public override void SetUp()
        {
            base.SetUp();
            DestroyBreakupTransients();
            savedMaxDelta = Time.maximumDeltaTime;
            Time.maximumDeltaTime = .05f;
            host = new GameObject("[BreakupTestServices]");
            units = host.AddComponent<UnitService>();
            ShipServices.Compose(units, host.transform, presentationEnabled: true);
        }

        public override void TearDown()
        {
            units.Clear();
            Object.DestroyImmediate(host);
            DestroyBreakupTransients();
            Time.maximumDeltaTime = savedMaxDelta;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator ValisDeath_RestPoseIsContinuous() => ValisDeath(0f);

        [UnityTest]
        public IEnumerator ValisDeath_SweptPoseIsContinuous() => ValisDeath(.6f);

        [UnityTest]
        public IEnumerator ValisDeath_IntermediatePoseIsContinuous() => ValisDeath(.2f);

        [UnityTest]
        public IEnumerator CrimsonDeath_RetainsAuthoredBreakup() => FixedHullDeath("Crimson");

        [UnityTest]
        public IEnumerator VanguardDeath_RetainsAuthoredBreakup() => FixedHullDeath("Vanguard");

        [UnityTest]
        public IEnumerator NightshadeDeath_PreservesPaintAndBreaksIntoLargeSections()
        {
            var ship = Spawn("Nightshade");
            yield return null;
            var visual = ship.GetComponentInChildren<ShipBreakupVisual>();
            var hull = (Transform)new SerializedObject(visual).FindProperty("hull").objectReferenceValue;
            var before = PaintedCorners(hull.GetComponentsInChildren<MeshFilter>());
            var velocity = new Vector3(.7f, -.3f, 0f);
            ship.Rigidbody.linearVelocity = velocity;
            Kill(ship);
            var debris = Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None).Single();
            var pieces = debris.GetComponentsInChildren<MeshFilter>();
            Assert.That(pieces, Has.Length.EqualTo(13));
            Assert.That(hull.gameObject.activeSelf, Is.False);
            Assert.That(Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None)
                .Count(e => e.gameObject.activeInHierarchy), Is.EqualTo(1));
            var after = PaintedCorners(pieces);
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            foreach (var material in before.Keys)
            {
                Assert.That(after[material], Has.Count.EqualTo(before[material].Count), material);
                var buckets = before[material].GroupBy(c => Vector3Int.RoundToInt(c.position / .0001f))
                    .ToDictionary(g => g.Key, g => g.ToList());
                foreach (var corner in after[material])
                {
                    var key = Vector3Int.RoundToInt(corner.position / .0001f);
                    var matched = false;
                    for (var x = -1; x <= 1 && !matched; x++)
                    for (var y = -1; y <= 1 && !matched; y++)
                    for (var z = -1; z <= 1 && !matched; z++)
                    {
                        if (!buckets.TryGetValue(key + new Vector3Int(x, y, z), out var candidates)) continue;
                        var index = candidates.FindIndex(c => Vector3.Distance(c.position, corner.position) < .0001f &&
                            Vector2.Distance(c.uv, corner.uv) < .00001f);
                        if (index < 0) continue;
                        candidates.RemoveAt(index);
                        matched = true;
                    }
                    if (!matched) Assert.Fail(material + " painted corner moved: " + corner);
                }
            }
            yield return VerifyMotionFadeAndCleanup(debris, velocity);
            ship.ResetShip();
            yield return null;
            Assert.That(hull.gameObject.activeInHierarchy, Is.True);
            Kill(ship);
            Assert.That(Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(hull.gameObject.activeSelf, Is.False);
        }

        private static Dictionary<string, List<(Vector3 position, Vector2 uv)>> PaintedCorners(IEnumerable<MeshFilter> filters)
        {
            var result = new Dictionary<string, List<(Vector3 position, Vector2 uv)>>();
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                var materials = filter.GetComponent<Renderer>().sharedMaterials;
                using var data = MeshUtility.AcquireReadOnlyMeshData(mesh);
                using var positions = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
                using var uv = new NativeArray<Vector2>(data[0].vertexCount, Allocator.Temp);
                data[0].GetVertices(positions);
                data[0].GetUVs(0, uv);
                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    var material = materials[submesh].name;
                    if (!result.TryGetValue(material, out var corners))
                        result[material] = corners = new List<(Vector3, Vector2)>();
                    using var indices = new NativeArray<int>(data[0].GetSubMesh(submesh).indexCount, Allocator.Temp);
                    data[0].GetIndices(indices, submesh);
                    foreach (var index in indices)
                        corners.Add((filter.transform.TransformPoint(positions[index]), uv[index]));
                }
            }
            return result;
        }

        [UnityTest]
        public IEnumerator ValisRevive_RestoresHullAndBreaksAgain()
        {
            var ship = Spawn("Valis");
            yield return null;
            var hull = ship.GetComponentInChildren<SkinnedMeshRenderer>();
            Kill(ship);
            yield return new WaitForSeconds(1.6f);
            ship.ResetShip();
            yield return null;
            Assert.That(hull.gameObject.activeInHierarchy, Is.True);
            Kill(ship);
            Assert.That(Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(hull.gameObject.activeSelf, Is.False);
        }

        private Ship Spawn(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/" + name + ".prefab");
            return units.SpawnShip(prefab, null, 0, new Vector3(3f, -2f), Quaternion.Euler(0f, 0f, 31f), Field);
        }

        private IEnumerator ValisDeath(float sweepSeconds)
        {
            var ship = Spawn("Valis");
            yield return null;
            if (sweepSeconds > 0f)
            {
                ship.Movement.Drive(new Ships.Command.PilotCommand { thrust = 1f });
                yield return new WaitForSeconds(sweepSeconds);
            }
            var hull = ship.GetComponentInChildren<SkinnedMeshRenderer>();
            var angle = Quaternion.Angle(Quaternion.identity, hull.bones[1].localRotation);
            if (sweepSeconds == 0f) Assert.That(angle, Is.EqualTo(20f).Within(.05f));
            else if (sweepSeconds >= .5f) Assert.That(angle, Is.LessThan(.05f));
            else Assert.That(angle, Is.InRange(2f, 18f));
            var baked = new Mesh();
            hull.BakeMesh(baked, true);
            var before = baked.vertices.Select(hull.transform.TransformPoint).ToArray();
            Object.DestroyImmediate(baked);
            var materials = hull.sharedMaterials;
            var velocity = new Vector3(.7f, -.3f, 0f);
            ship.Rigidbody.linearVelocity = velocity;
            Kill(ship);
            var debris = Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None).Single();
            Assert.That(hull.gameObject.activeSelf, Is.False);
            var effects = Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None)
                .Where(e => e.gameObject.activeInHierarchy).ToArray();
            Assert.That(effects, Has.Length.EqualTo(1));
            Assert.That(effects[0].name, Does.StartWith("LayeredAsteroidExplosion"));
            var pieces = debris.GetComponentsInChildren<MeshFilter>();
            Assert.That(pieces, Has.Length.EqualTo(14));
            var after = pieces.SelectMany(p => p.sharedMesh.vertices.Select(p.transform.TransformPoint)).ToArray();
            AssertGeometry(before, after);
            foreach (var piece in pieces)
                Assert.That(piece.GetComponent<Renderer>().sharedMaterials, Is.EqualTo(materials));
            var sourceUv = hull.sharedMesh.uv.OrderBy(v => v.x).ThenBy(v => v.y).ToArray();
            var debrisUv = pieces.SelectMany(p => p.sharedMesh.uv).OrderBy(v => v.x).ThenBy(v => v.y).ToArray();
            Assert.That(debrisUv, Is.EqualTo(sourceUv));
            yield return VerifyMotionFadeAndCleanup(debris, velocity);
        }

        private IEnumerator FixedHullDeath(string name)
        {
            var ship = Spawn(name);
            yield return null;
            var visual = ship.GetComponentInChildren<ShipBreakupVisual>();
            var serialized = new SerializedObject(visual);
            var hull = (Transform)serialized.FindProperty("hull").objectReferenceValue;
            var expected = hull.position;
            Kill(ship);
            var debris = Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None).Single();
            Assert.That(visual.gameObject.activeInHierarchy, Is.False);
            Assert.That(hull.gameObject.activeSelf, Is.False);
            Assert.That(Vector3.Distance(debris.transform.position, expected), Is.LessThan(.00001f));
            Assert.That(debris.PoseCount, Is.Zero);
            yield return VerifyMotionFadeAndCleanup(debris, Vector3.zero);
        }

        // Debris and bursts outlive whichever fixture's ship died; these tests count them globally.
        private static void DestroyBreakupTransients()
        {
            foreach (var debris in Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None))
                Object.DestroyImmediate(debris.gameObject);
            foreach (var effect in Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None))
                Object.DestroyImmediate(effect.gameObject);
            SimplePool<PooledVFX>.Clear();
        }

        private static void Kill(Ship ship) => ship.Damage.TakeDamage(new DamageInfo(
            100000f, DamageKind.Collision, ShipId.Invalid, 0f, Vector3.zero, ship.transform.position));

        private static IEnumerator VerifyMotionFadeAndCleanup(ShipBreakupDebris debris, Vector3 velocity)
        {
            Assert.That(Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None)
                .Any(e => e.gameObject.activeInHierarchy && e.GetComponent<ParticleSystem>().isPlaying), Is.True);
            var authoredPieces = new SerializedObject(debris).FindProperty("pieces");
            var renderer = (Renderer)Enumerable.Range(0, authoredPieces.arraySize)
                .Select(authoredPieces.GetArrayElementAtIndex)
                .OrderByDescending(p => p.FindPropertyRelative("lifetime").floatValue)
                .First().FindPropertyRelative("renderer").objectReferenceValue;
            var startPiece = debris.transform.InverseTransformPoint(renderer.transform.position);
            var startRoot = debris.transform.position;
            var startTime = Time.time;
            yield return new WaitForSeconds(.2f);
            yield return null;
            Assert.That(Vector3.Distance(debris.transform.InverseTransformPoint(renderer.transform.position), startPiece), Is.GreaterThan(.01f));
            Assert.That(Vector3.Distance(debris.transform.position, startRoot + velocity * (Time.time - startTime)), Is.LessThan(.003f));
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat(Visibility), Is.EqualTo(1f));
            yield return new WaitForSeconds(1.05f);
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat(Visibility), Is.InRange(.01f, .99f));
            yield return new WaitForSeconds(.3f);
            Assert.That(debris == null, Is.True);
        }

        private static void AssertGeometry(Vector3[] before, Vector3[] after)
        {
            Assert.That(after, Has.Length.EqualTo(before.Length));
            const float tolerance = .00002f;
            var buckets = new Dictionary<Vector3Int, List<Vector3>>();
            foreach (var point in after)
            {
                var key = Vector3Int.RoundToInt(point / tolerance);
                if (!buckets.TryGetValue(key, out var points)) buckets.Add(key, points = new List<Vector3>());
                points.Add(point);
            }
            foreach (var point in before)
            {
                var key = Vector3Int.RoundToInt(point / tolerance);
                var matched = false;
                for (var x = -1; x <= 1 && !matched; x++)
                    for (var y = -1; y <= 1 && !matched; y++)
                        for (var z = -1; z <= 1 && !matched; z++)
                            if (buckets.TryGetValue(key + new Vector3Int(x, y, z), out var points))
                                matched = points.Any(p => Vector3.Distance(p, point) < tolerance);
                if (!matched) Assert.Fail("Debris omitted or moved a visible hull vertex at death: " + point);
            }
        }
    }
}
