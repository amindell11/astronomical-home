#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Asteroids;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Damage;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Keystones
{
    [Category("Ships")]
    public class RamDashPlayModeTests : PlayModeWorldFixture
    {
        private const string BulwarkPath = "Assets/Prefabs/Ships/Bulwark.prefab";
        private const string VanguardPath = "Assets/Prefabs/Ships/Vanguard.prefab";
        private const string SpawnSettingsPath = "Assets/Settings/Asteroids/SpawnSettings.asset";
        private const string FragSettingsPath = "Assets/Settings/Asteroids/FragSettings.asset";
        private const float RockScale = 1.65f;
        private const int MaxSteps = 60;

        private static readonly Vector3 Nose = GamePlane.PlaneDirToWorld(Vector2.up);

        private readonly List<GameObject> spawned = new();
        private Ship bulwark;
        private Ship target;
        private RamDash ram;

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(bulwark);
            ShipTestFactory.DestroyShip(target);
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator LighterRock_Shatters_AndTheContactLeavesTheBulwarkUntouched()
        {
            yield return SpawnBulwark();
            var fragments = 0;
            var rock = SpawnRock(Nose * 5f, mass: 1000f, onFragment: () => fragments++);
            var health = bulwark.Damage.Health.CurrentValue;
            var shield = bulwark.Damage.Shield.CurrentValue;

            yield return RamUntilResolved();

            Assert.That(rock.Damage.Health, Is.LessThanOrEqualTo(0f), "a rock lighter than the Bulwark breaks");
            Assert.That(fragments, Is.GreaterThan(0), "and leaves the Bulwark in its debris");
            Assert.That(bulwark.Damage.Health.CurrentValue, Is.EqualTo(health), "the contact step is immune");
            Assert.That(bulwark.Damage.Shield.CurrentValue, Is.EqualTo(shield));
            Assert.That(ram.PaidActivations, Is.EqualTo(1), "breaking a rock pays");
        }

        [UnityTest]
        public IEnumerator HeavierRock_StopsTheBulwark_AndBothStayIntact()
        {
            yield return SpawnBulwark();
            var rock = SpawnRock(Nose * 5f, mass: 20000f, onFragment: null);
            var health = bulwark.Damage.Health.CurrentValue;
            var shield = bulwark.Damage.Shield.CurrentValue;

            yield return RamUntilResolved();
            for (var i = 0; i < 5; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(rock.gameObject.activeSelf && rock.Damage.Health > 0f, Is.True, "a heavier rock survives");
            Assert.That(bulwark.Damage.Health.CurrentValue, Is.EqualTo(health), "the bounce costs nothing");
            Assert.That(bulwark.Damage.Shield.CurrentValue, Is.EqualTo(shield));
            Assert.That(Vector3.Dot(bulwark.Rigidbody.linearVelocity, Nose), Is.LessThan(2f), "the rock stopped the ram");
            Assert.That(ram.PaidActivations, Is.EqualTo(0), "a bounce does not pay");
        }

        [UnityTest]
        public IEnumerator Ship_TakesRamDamageScaledByTheHit_CreditedToTheBulwark_Once()
        {
            yield return SpawnBulwark();
            target = Object.Instantiate(TestAssets.Load<Ship>(VanguardPath), Nose * 4.5f, GamePlane.PlanePose(GamePlane.Normal, -Nose));
            target.Initialize(1, 0, Projectiles);
            var hits = new List<AppliedDamage>();
            target.Damage.OnDamaged += hits.Add;
            yield return new WaitForFixedUpdate();

            yield return RamUntilResolved();
            for (var i = 0; i < 10; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(hits, Has.Count.EqualTo(1), "one ram, one hit");
            Assert.That(hits[0].Info.Kind, Is.EqualTo(DamageKind.Collision));
            Assert.That(hits[0].Info.AttackerId, Is.EqualTo(bulwark.Id), "the ram carries the Bulwark's id");
            Assert.That(hits[0].Info.Amount, Is.InRange(ram.ShipDamage * 0.8f, ram.ShipDamage), "a near head-on hit deals near full damage");
            Assert.That(ram.PaidActivations, Is.EqualTo(1), "landing on a ship pays");
        }

        [UnityTest]
        public IEnumerator RamIntoAShip_DoesNotPin_TheShipsSeparate()
        {
            yield return SpawnBulwark();
            target = Object.Instantiate(TestAssets.Load<Ship>(VanguardPath), Nose * 4.5f, GamePlane.PlanePose(GamePlane.Normal, -Nose));
            target.Initialize(1, 0, Projectiles);
            yield return new WaitForFixedUpdate();
            var contactRange = BumperRadius(bulwark) + BumperRadius(target) + 0.5f;

            yield return RamUntilResolved();
            var longestRun = 0;
            var run = 0;
            for (var i = 0; i < 100; i++)
            {
                bulwark.Movement.Drive(default);
                yield return new WaitForFixedUpdate();
                run = Vector3.Distance(bulwark.Rigidbody.position, target.Rigidbody.position) < contactRange ? run + 1 : 0;
                longestRun = Mathf.Max(longestRun, run);
            }

            Assert.That(longestRun * Time.fixedDeltaTime, Is.LessThan(0.5f), "the ram is a hit, not a sustained push");
        }

        private IEnumerator SpawnBulwark()
        {
            bulwark = Object.Instantiate(TestAssets.Load<Ship>(BulwarkPath), Vector3.zero, GamePlane.PlanePose(GamePlane.Normal, Nose));
            bulwark.Initialize(0, 0, Projectiles);
            yield return new WaitForFixedUpdate();
            ram = bulwark.Keystone as RamDash;
            Assert.That(ram, Is.Not.Null, "Bulwark carries the ram");
        }

        private IEnumerator RamUntilResolved()
        {
            bulwark.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(ram.RamLive, Is.True, "the dash armed the ram");
            for (var i = 0; i < MaxSteps && ram.RamLive; i++)
            {
                bulwark.Movement.Drive(default);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(ram.RamLive, Is.False, "the ram resolved");
            Assert.That(ram.Blocks(new DamageInfo(10f, DamageKind.Collision, default, 0f, Vector3.zero, Vector3.zero)), Is.False,
                "collision immunity ends with the ram");
        }

        private AsteroidController SpawnRock(Vector3 at, float mass, System.Action onFragment)
        {
            var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(SpawnSettingsPath);
            var holder = new GameObject("Asteroid spawner");
            spawned.Add(holder);
            holder.SetActive(false);
            SetReference(holder.AddComponent<Fragger>(), "asteroidFragAsteroidFragSettings",
                AssetDatabase.LoadAssetAtPath<AsteroidFragSettings>(FragSettingsPath));
            var spawner = holder.AddComponent<AsteroidSpawner>();
            SetReference(spawner, "settings", settings);
            if (onFragment != null) spawner.OnFragmentSpawned += _ => onFragment();
            holder.SetActive(true);

            var info = settings.meshInfos[0];
            var attributes = new AsteroidAttributes(info, 0, mass, RockScale, Vector3.zero, Vector3.zero);
            var rock = spawner.Spawn(new Pose(at, Quaternion.identity), attributes, 713);
            Assert.That(rock.Mass, Is.EqualTo(mass));
            return rock;
        }

        private static float BumperRadius(Ship ship)
        {
            var bumper = ship.GetComponentInChildren<SphereCollider>();
            var scale = bumper.transform.lossyScale;
            return bumper.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
