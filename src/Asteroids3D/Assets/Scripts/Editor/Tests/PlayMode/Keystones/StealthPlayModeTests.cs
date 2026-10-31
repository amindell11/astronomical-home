#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using AI.Strategy;
using Combat;
using Combat.Projectiles;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones.Kits;
using Substrate;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Keystones
{
    [Category("AI")]
    public class StealthPlayModeTests : AIIntegrationFixture
    {
        private const string NightshadePath = "Assets/Prefabs/Ships/Nightshade.prefab";
        private const string MissilePath = "Assets/Prefabs/Weapons/Projectiles/MissileProjectile.prefab";

        private readonly List<GameObject> spawned = new();
        private Ship nightshade;
        private Stealth stealth;

        private sealed class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public global::Ships.Registry.ShipId Id => global::Ships.Registry.ShipId.Invalid;
        }

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(nightshade);
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Stealth_BreaksAMissilesTrack_OnTheStepItStarts_AndPays()
        {
            yield return SpawnNightshade(Vector3.zero, team: 1);
            var missile = LaunchTrackingMissile(GamePlane.WorldPointToPlane(nightshade.transform.position) - new Vector2(0f, 9f));
            yield return new WaitForFixedUpdate();
            Assert.That(nightshade.Lock.Trackers, Has.Count.EqualTo(1));

            nightshade.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();

            Assert.That(stealth.Active, Is.True);
            Assert.That(nightshade.Lock.IsConcealed, Is.True);
            Assert.That(nightshade.Lock.Trackers, Is.Empty, "the track breaks on the step stealth starts");
            Assert.That(missile.gameObject.activeSelf, Is.True, "the missile flies on, lost");
            Assert.That(stealth.PaidActivations, Is.EqualTo(1), "breaking a track pays");
        }

        [UnityTest]
        public IEnumerator ScriptedEnemy_LosesTheNightshade_AndItsCombatExitGraceRuns()
        {
            var (_, cmdr) = CreateAIShip(Vector3.zero, team: 0);
            yield return SpawnNightshade(new Vector3(12f, 0f, 0f), team: 1);

            var deadline = Time.realtimeSinceStartup + 5f;
            while (cmdr.context?.Combat.HasEnemy != true && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
            var ctx = cmdr.context;
            Assert.That(ctx?.Combat.HasEnemy, Is.True, "the enemy acquires the Nightshade first");

            nightshade.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(stealth.Active, Is.True);

            cmdr.enabled = false;
            ctx.Update(0.05f);
            Assert.That(ctx.Combat.HasEnemy, Is.False, "a concealed enemy drops out of the tracker");
            ctx.Update(1f);
            Assert.That(ctx.Combat.InCombat, Is.True, "the combat-exit grace keeps it in combat a while");
            ctx.Update(3f);
            Assert.That(ctx.Combat.InCombat, Is.False, "and lets it go once the grace runs out");
        }

        [UnityTest]
        public IEnumerator ScriptedEnemy_SearchesWhereTheNightshadeWasLastSeen_WithGunsCold()
        {
            var (_, cmdr) = CreateAIShip(Vector3.zero, team: 0);
            yield return SpawnNightshade(new Vector3(12f, 0f, 0f), team: 1);
            var brain = cmdr.InstallBrain<ScriptedBrain>();
            brain.decision = new BrainDecision(NavObjective.Anchored(nightshade.Id).Position(0f, 0f, 6f, 1f), engagePrimary: true);
            yield return new WaitForFixedUpdate();
            Assert.That(cmdr.Gunner.HasTarget, Is.True, "the enemy engages the Nightshade first");

            nightshade.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(stealth.Active, Is.True);
            var lastSeen = nightshade.Kinematics.pos;

            brain.decision = null;
            nightshade.Rigidbody.position = GamePlane.PlanePointToWorld(new Vector2(-30f, 0f));
            for (var i = 0; i < 5; i++)
                yield return new WaitForFixedUpdate();

            var navigator = cmdr.Navigator;
            Assert.That(navigator.ShouldIdle(), Is.False, "the enemy keeps flying instead of freezing");
            Assert.That(Vector2.Distance(new Vector2(navigator.enemyPos.x, navigator.enemyPos.y), lastSeen), Is.LessThan(0.1f),
                "it flies to where it last saw the Nightshade, not where it went");
            Assert.That(navigator.enemyVel.x == 0f && navigator.enemyVel.y == 0f, "the last-seen point holds still");
            Assert.That(cmdr.Gunner.HasTarget, Is.False, "and its guns stay cold");
        }

        [UnityTest]
        public IEnumerator Stealth_FliesPastTheBaseCap_WhileConcealed()
        {
            yield return SpawnNightshade(Vector3.zero, team: 1);
            var baseCap = nightshade.Stats.maxSpeed;
            var full = new PilotCommand { thrust = 1f };
            for (var i = 0; i < 200; i++)
            {
                nightshade.Movement.Drive(full);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(nightshade.Kinematics.Speed, Is.LessThanOrEqualTo(baseCap + 0.01f), "the base cap holds before stealth");

            nightshade.Movement.Drive(new PilotCommand { thrust = 1f, boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(stealth.Active, Is.True);

            var peak = 0f;
            while (stealth.Active)
            {
                nightshade.Movement.Drive(full);
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, nightshade.Kinematics.Speed);
            }
            Assert.That(peak, Is.GreaterThan(baseCap * 1.1f), "concealed, it flies well past the base cap");
            Assert.That(nightshade.Dynamics.maxSpeed, Is.EqualTo(baseCap).Within(0.01f), "the cap returns to base when stealth ends");
        }

        [UnityTest]
        public IEnumerator Firing_EndsStealth_AndTheWindowEndsItOtherwise()
        {
            yield return SpawnNightshade(Vector3.zero, team: 1);
            nightshade.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(stealth.Active, Is.True);

            var weapons = nightshade.Weapons.Arm(Projectiles);
            var aim = nightshade.transform.position + GamePlane.PlaneDirToWorld(Vector2.up) * 20f;
            for (var i = 0; i < 10 && stealth.Active; i++)
            {
                weapons.Fire(WeaponSlot.Primary, new WeaponCommand { held = true, pressed = i == 0, targetPoint = aim });
                yield return new WaitForFixedUpdate();
            }
            Assert.That(stealth.Active, Is.False, "the first shot ends stealth");
            Assert.That(nightshade.Lock.IsConcealed, Is.False);

            stealth.ResetKeystone();
            nightshade.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(stealth.Active, Is.True);
            var steps = Mathf.CeilToInt(stealth.Window / Time.fixedDeltaTime) + 2;
            for (var i = 0; i < steps; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(stealth.Active, Is.False, "unfired, it ends with its window");
        }

        private IEnumerator SpawnNightshade(Vector3 at, int team)
        {
            nightshade = Object.Instantiate(TestAssets.Load<Ship>(NightshadePath), at, GamePlane.Rotation);
            nightshade.Initialize(team, 0, Projectiles);
            registry.ActiveShips.Add(nightshade);
            yield return new WaitForFixedUpdate();
            stealth = nightshade.Keystone as Stealth;
            Assert.That(stealth, Is.Not.Null, "Nightshade carries Stealth");
        }

        private Missile LaunchTrackingMissile(Vector2 from)
        {
            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            spawned.Add(shooter.gameObject);
            var heading = GamePlane.PlaneDirToWorld(Vector2.up);
            var missile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Missile>(MissilePath),
                GamePlane.PlanePointToWorld(from), GamePlane.PlanePose(GamePlane.Normal, heading));
            spawned.Add(missile.gameObject);
            missile.Initialize(shooter);
            missile.Launch(heading, nightshade.transform.position);
            missile.SetTarget(nightshade);
            return missile;
        }
    }
}
#endif
