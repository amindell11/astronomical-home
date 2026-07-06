#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using AI.Scanning;
using Asteroids;
using Asteroids.Spawning;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Track B2: the ObstacleScanner's deterministic registry query. Verifies the fixed-AABB
    /// asteroid query against a real spawner (membership, authoritative radius, live-state
    /// despawn), its speed independence, and that the physics fallback still runs when no
    /// spawner is live.
    /// </summary>
    [TestFixture]
    [Category("Scanning")]
    public class DeterministicSensingPlayModeTests : PlayModeWorldFixture
    {
        private const string SpawnSettingsPath = "Assets/Settings/Asteroids/SpawnSettings.asset";

        private readonly List<GameObject> created = new();
        private AsteroidSpawner spawner;
        private Transform scanOrigin;

        public override void SetUp()
        {
            base.SetUp();
            scanOrigin = Track(new GameObject("ScanOrigin")).transform;
        }

        public override void TearDown()
        {
            if (spawner) spawner.DespawnAll();
            foreach (var go in created)
                if (go) Object.DestroyImmediate(go);
            created.Clear();
            spawner = null;
            base.TearDown();
        }

        private GameObject Track(GameObject go)
        {
            created.Add(go);
            return go;
        }

        private AsteroidSpawner CreateSpawner()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(SpawnSettingsPath);
            Assert.IsNotNull(settings, $"Missing spawn settings at {SpawnSettingsPath}");

            var go = Track(new GameObject("Spawner"));
            go.SetActive(false);
            var sp = go.AddComponent<AsteroidSpawner>();
            using (var so = new SerializedObject(sp))
            {
                so.FindProperty("settings").objectReferenceValue = settings;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            go.SetActive(true); // Awake with settings assigned
            return sp;
        }

        private AsteroidController SpawnAt(Vector2 planePos)
        {
            var settings = spawner.Settings;
            var attrs = new AsteroidAttributes(
                settings.meshInfos[0], mass: 100f, scale: 1f,
                velocity: Vector3.zero, angularVelocity: Vector3.zero);
            var world = Game.GamePlane.PlanePointToWorld(planePos);
            return spawner.Spawn(new Pose(world, Quaternion.identity), attrs);
        }

        private ObstacleScanner MakeScanner(float lookahead = 2f)
        {
            var scanner = new ObstacleScanner(scanOrigin, ~0, lookahead);
            scanner.MaxAccel = 0f; // envelope = maxSpeed * lookahead exactly
            return scanner;
        }

        [UnityTest]
        public IEnumerator RegistryQuery_FindsLiveAsteroidsInFixedAabb()
        {
            spawner = CreateSpawner();
            yield return null;

            var half = 20f; // maxSpeed 10 * lookahead 2
            var inside = SpawnAt(new Vector2(5f, 5f));
            var edgeOut = SpawnAt(new Vector2(half + 5f, 0f));
            var farOut = SpawnAt(new Vector2(200f, 200f));
            yield return new WaitForFixedUpdate();

            var scanner = MakeScanner();
            scanner.Scan(Vector2.zero, maxSpeed: 10f);

            Assert.IsTrue(scanner.UsedRegistryQuery, "Spawner live: must use the registry query");
            Assert.AreEqual(half, scanner.Radius, 1e-3f, "AABB half-extent must be the fixed envelope");

            var detected = Collect(scanner);
            Assert.IsTrue(detected.ContainsKey(inside), "Asteroid inside the AABB must be reported");
            Assert.IsFalse(detected.ContainsKey(edgeOut), "Asteroid outside the AABB must not be reported");
            Assert.IsFalse(detected.ContainsKey(farOut), "Distant asteroid must not be reported");
            Assert.AreEqual(inside.Radius, detected[inside].radius, 1e-3f,
                "Reported radius must be the controller's authoritative in-plane radius");
            Assert.IsNotNull(detected[inside].collider, "Contract: DetectedObstacle.collider populated");
        }

        [UnityTest]
        public IEnumerator RegistryQuery_ReflectsDespawnedAsteroids()
        {
            spawner = CreateSpawner();
            yield return null;

            var a = SpawnAt(new Vector2(3f, 0f));
            var b = SpawnAt(new Vector2(-3f, 0f));
            yield return new WaitForFixedUpdate();

            var scanner = MakeScanner();
            scanner.Scan(Vector2.zero, 10f);
            Assert.AreEqual(2, scanner.DetectedCount, "Both live asteroids must be reported");

            spawner.Despawn(a);
            scanner.Scan(Vector2.zero, 10f);
            var detected = Collect(scanner);
            Assert.AreEqual(1, scanner.DetectedCount, "Despawned asteroid must leave the scan immediately");
            Assert.IsTrue(detected.ContainsKey(b));
        }

        [UnityTest]
        public IEnumerator RegistryQuery_ExtentIndependentOfCurrentSpeed()
        {
            spawner = CreateSpawner();
            yield return null;
            SpawnAt(new Vector2(15f, 0f));
            yield return new WaitForFixedUpdate();

            var scanner = MakeScanner();
            scanner.Scan(Vector2.zero, 10f);
            var slowCount = scanner.DetectedCount;
            var slowRadius = scanner.Radius;

            scanner.Scan(new Vector2(10f, 0f), 10f);
            Assert.AreEqual(slowCount, scanner.DetectedCount,
                "Registry query must not breathe with current speed");
            Assert.AreEqual(slowRadius, scanner.Radius, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Fallback_NoLiveSpawner_UsesPhysicsScan()
        {
            Assert.AreEqual(0, AsteroidSpawner.ActiveSpawners.Count,
                "Precondition: no live spawners for the fallback path");

            var rock = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            rock.transform.position = scanOrigin.position + new Vector3(3f, 0f, 0f);
            yield return new WaitForFixedUpdate();

            var scanner = MakeScanner();
            scanner.Scan(new Vector2(5f, 0f), 10f);

            Assert.IsFalse(scanner.UsedRegistryQuery, "No spawner: must fall back to physics");
            Assert.AreEqual(1, scanner.DetectedCount, "Physics fallback must detect the primitive obstacle");
        }

        private static Dictionary<AsteroidController, DetectedObstacle> Collect(ObstacleScanner scanner)
        {
            var map = new Dictionary<AsteroidController, DetectedObstacle>();
            for (var i = 0; i < scanner.DetectedCount; i++)
            {
                var d = scanner.DetectedBuffer[i];
                var ast = d.collider ? d.collider.GetComponentInParent<AsteroidController>() : null;
                if (ast) map[ast] = d;
            }
            return map;
        }
    }
}
#endif
