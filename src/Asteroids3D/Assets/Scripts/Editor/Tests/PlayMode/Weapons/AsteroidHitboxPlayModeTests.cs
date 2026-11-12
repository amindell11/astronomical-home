#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Asteroids;
using Combat;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Damage;
using NUnit.Framework;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons
{
    /// <summary>Pins that bolts and the railgun beam hit an asteroid's hull, never the trigger sphere poking past it.</summary>
    [Category("Weapons")]
    public class AsteroidHitboxPlayModeTests : PlayModeWorldFixture
    {
        private const string LaserPrefabPath = "Assets/Prefabs/Weapons/Projectiles/Laser.prefab";
        private const string RipperSlugPrefabPath = "Assets/Prefabs/Weapons/Projectiles/RipperSlug.prefab";
        private const string RailgunPrefabPath = "Assets/Prefabs/Weapons/Railgun.prefab";

        private const float HullHalfLength = 2f;
        private const float HullHalfWidth = 0.5f;
        private const float SphereRadius = 1.5f;
        // Clears the hull for a bolt and for the railgun's 0.5 beam radius, yet crosses the sphere for both.
        private const float PastHullInsideSphere = 1.25f;
        private const int MaxFlightSteps = 30;

        private static readonly Vector3 Aim = GamePlane.PlaneDirToWorld(Vector2.up);
        private static readonly Vector3 Across = GamePlane.PlaneDirToWorld(Vector2.right);

        private readonly List<Object> spawned = new();
        private int rockHits;
        private float savedCaptureDelta;

        private sealed class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public Ships.Registry.ShipId Id => Ships.Registry.ShipId.Invalid;
        }

        public override void SetUp()
        {
            base.SetUp();
            savedCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            rockHits = 0;
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = savedCaptureDelta;
            foreach (var obj in spawned)
                if (obj) Object.DestroyImmediate(obj);
            spawned.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Laser_ThroughTheSpherePastTheHull_DealsNoDamageAndRaisesNoHit() =>
            BoltPastTheHull(LaserPrefabPath);

        [UnityTest]
        public IEnumerator RipperSlug_ThroughTheSpherePastTheHull_DealsNoDamageAndRaisesNoHit() =>
            BoltPastTheHull(RipperSlugPrefabPath);

        [UnityTest]
        public IEnumerator Laser_IntoTheHull_HitsAtTheHullFace() => BoltIntoTheHull(LaserPrefabPath);

        [UnityTest]
        public IEnumerator RipperSlug_IntoTheHull_HitsAtTheHullFace() => BoltIntoTheHull(RipperSlugPrefabPath);

        [Test]
        public void Railgun_ThroughTheSpherePastTheHull_PassesUnharmed()
        {
            CreateRock(Aim * 10f + Across * PastHullInsideSphere);
            var railgun = SpawnRailgun(out var hits, out var beam);

            Fire(railgun);

            Assert.That(hits, Is.Empty, "the beam raised no Hit");
            Assert.That(rockHits, Is.Zero, "the rock took no damage");
            Assert.AreEqual(railgun.BeamRange, Vector3.Distance(beam[0], beam[1]), 0.01f, "the beam ran its full range");
        }

        [Test]
        public void Railgun_IntoTheHull_StopsAtTheHullFace()
        {
            var rock = CreateRock(Aim * 10f);
            var railgun = SpawnRailgun(out var hits, out _);

            Fire(railgun);

            Assert.That(hits, Has.Count.EqualTo(1));
            Assert.AreSame(rock.Damage, hits[0].target);
            Assert.AreEqual(10f - HullHalfLength, Vector3.Dot(hits[0].point, Aim), 0.1f, "the strike lands on the hull's near face");
        }

        private IEnumerator BoltPastTheHull(string prefabPath)
        {
            CreateRock(Vector3.zero);
            var bolt = FireBolt(prefabPath, Across * PastHullInsideSphere);
            var hits = 0;
            bolt.Hit += (_, _) => hits++;

            for (var i = 0; i < MaxFlightSteps && Vector3.Dot(bolt.transform.position, Aim) <= HullHalfLength + SphereRadius; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(hits, Is.Zero, "the bolt raised no Hit");
            Assert.That(rockHits, Is.Zero, "the rock took no damage");
            Assert.That(Vector3.Dot(bolt.transform.position, Aim), Is.GreaterThan(HullHalfLength + SphereRadius),
                "the bolt flew past the rock");
        }

        private IEnumerator BoltIntoTheHull(string prefabPath)
        {
            var rock = CreateRock(Vector3.zero);
            var bolt = FireBolt(prefabPath, Vector3.zero);
            var hits = new List<(Vector3 point, IDamageable target)>();
            bolt.Hit += (point, target) => hits.Add((point, target));

            for (var i = 0; i < MaxFlightSteps && hits.Count == 0; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(hits, Has.Count.EqualTo(1));
            Assert.AreSame(rock.Damage, hits[0].target);
            Assert.That(rockHits, Is.EqualTo(1));
            Assert.AreEqual(-HullHalfLength, Vector3.Dot(hits[0].point, Aim), 0.3f, "the hit lands at the hull's near face");
        }

        // No spawner means no anchor, so the collider LOD keeps the hull live, as near the player.
        private AsteroidController CreateRock(Vector3 position)
        {
            var go = new GameObject("ElongatedRock") { layer = LayerIds.Asteroid };
            go.SetActive(false);
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<Rigidbody>().isKinematic = true;
            var hull = go.AddComponent<MeshCollider>();
            hull.convex = true;
            hull.sharedMesh = BoxMesh();
            var rock = go.AddComponent<AsteroidController>();
            var sphere = go.GetComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = SphereRadius;
            go.SetActive(true);
            rock.Damage.Initialize(volume: 1000f);
            rock.Damage.OnHit += _ => rockHits++;
            Physics.SyncTransforms();
            return rock;
        }

        private Mesh BoxMesh()
        {
            var vertices = new Vector3[8];
            for (var i = 0; i < 8; i++)
                vertices[i] = ((i & 1) == 0 ? -1f : 1f) * HullHalfLength * Aim
                    + ((i & 2) == 0 ? -1f : 1f) * HullHalfWidth * Across
                    + ((i & 4) == 0 ? -1f : 1f) * HullHalfWidth * GamePlane.Normal;
            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = new[]
                {
                    0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6,
                    0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7,
                    0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5,
                },
            };
            spawned.Add(mesh);
            return mesh;
        }

        private Laser FireBolt(string prefabPath, Vector3 acrossOffset)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Laser>(prefabPath);
            Assert.IsNotNull(prefab, $"Failed to load {prefabPath}");

            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            spawned.Add(shooter.gameObject);
            shooter.transform.position = Across * 10f;

            var start = acrossOffset - Aim * (HullHalfLength + SphereRadius + 1f);
            var bolt = Object.Instantiate(prefab, start, GamePlane.PlanePose(GamePlane.Normal, Aim));
            spawned.Add(bolt.gameObject);
            bolt.Initialize(shooter);
            bolt.Launch(Aim, start + Aim);
            return bolt;
        }

        private Railguns SpawnRailgun(out List<(Vector3 point, IDamageable target)> hits, out Vector3[] beam)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Railguns>(RailgunPrefabPath);
            Assert.IsNotNull(prefab, $"Failed to load {RailgunPrefabPath}");
            var railgun = Object.Instantiate(prefab, Vector3.zero, GamePlane.Rotation);
            spawned.Add(railgun.gameObject);
            var struck = new List<(Vector3 point, IDamageable target)>();
            var ends = new Vector3[2];
            railgun.Hit += (point, target) => struck.Add((point, target));
            railgun.OnBeamFired += (start, end) => (ends[0], ends[1]) = (start, end);
            hits = struck;
            beam = ends;
            return railgun;
        }

        private void Fire(Railguns railgun)
        {
            Physics.SyncTransforms();
            var fired = 0;
            railgun.OnFire += () => fired++;
            railgun.Charge.Configure(chargeTime: Time.fixedDeltaTime, minChargeToFire: 1f);
            railgun.HandleTrigger(new WeaponCommand { pressed = false, held = true }, Projectiles);
            Assert.AreEqual(1, fired, "one held step reaches full charge and fires");
        }
    }
}
#endif
