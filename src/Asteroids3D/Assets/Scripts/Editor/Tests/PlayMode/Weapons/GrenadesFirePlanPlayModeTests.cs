#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Weapons
{
    /// <summary>
    /// The Grenades AI trigger over the authored launcher, stepped the way the Gunner steps it: the
    /// launcher decides the trigger and launches on its press. It launches only at an aim point clear
    /// of its own blast, in range and in sight, and holds while one of its own charges or blasts is live.
    /// </summary>
    [Category("Weapons")]
    public class GrenadesFirePlanPlayModeTests : PlayModeWorldFixture
    {
        private const string GrenadesPrefabPath = "Assets/Prefabs/Weapons/Grenades.prefab";
        private const int MaxSteps = 500;

        protected override bool AccelerateTime => true;

        private readonly List<GameObject> spawned = new();
        private bool prevHeld;

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            prevHeld = false;
            base.TearDown();
        }

        private Grenades MountLauncher()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Grenades>(GrenadesPrefabPath);
            Assert.IsNotNull(prefab, $"Failed to load weapon prefab at {GrenadesPrefabPath}");
            var launcher = Object.Instantiate(prefab, Vector3.zero, GamePlane.Rotation);
            spawned.Add(launcher.gameObject);
            return launcher;
        }

        private static TargetingContext At(float distance, bool lineOfSight = true) => new()
        {
            distanceToTarget = distance,
            hasLineOfSight = lineOfSight,
        };

        /// <summary>One Gunner step toward a target <paramref name="distance"/> away along <paramref name="direction"/>; returns the charge it launched, if any.</summary>
        private Grenade Step(Grenades launcher, float distance, Vector2 direction, bool lineOfSight = true)
        {
            var held = launcher.ShouldFire(At(distance, lineOfSight));
            var pressed = held && !prevHeld;
            prevHeld = held;

            Grenade launched = null;
            void OnLaunched(IChargeFlight flight) => launched = (Grenade)flight;
            launcher.Launched += OnLaunched;
            var targetPoint = launcher.firePoint.position + GamePlane.PlaneDirToWorld(direction * distance);
            launcher.HandleTrigger(new WeaponCommand { held = held, pressed = pressed, targetPoint = targetPoint }, Projectiles);
            launcher.Launched -= OnLaunched;
            return launched;
        }

        private static bool Live(Component transient) => transient && transient.gameObject.activeSelf;

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Plan_LaunchesOnlyClearOfItsOwnBlast_InRangeAndInSight()
        {
            var launcher = MountLauncher();
            var minimum = launcher.MinLobDistance;
            Assert.Greater(minimum, launcher.BlastRadius, "the minimum lob distance clears the launcher's own blast");

            Assert.IsNull(Step(launcher, minimum - 0.5f, Vector2.right), "inside the minimum lob distance it holds");
            Assert.IsNull(Step(launcher, launcher.Range + 0.5f, Vector2.right), "past max range it holds");
            Assert.IsNull(Step(launcher, minimum + 0.5f, Vector2.right, lineOfSight: false), "out of sight it holds");
            Assert.IsNotNull(Step(launcher, minimum + 0.5f, Vector2.right), "clear of its blast, in range and in sight it launches");
        }

        [UnityTest]
        public IEnumerator Plan_HoldsWhileItsOwnChargeOrBlastIsLive_AndLaunchesOnceBothAreGone()
        {
            var launcher = MountLauncher();
            // No cooldown, so readiness never masks the hold.
            SetFloat(launcher.GetComponent<Cooldown>(), "fireRate", 0f);
            var distance = launcher.MinLobDistance + 2f;

            var first = Step(launcher, distance, Vector2.right);
            Assert.IsNotNull(first);
            ConcussionWave blast = null;
            first.Spawned += (child, _) => blast = child as ConcussionWave;

            var heldThroughBlast = false;
            Grenade second = null;
            for (var i = 0; i < MaxSteps && second == null; i++)
            {
                yield return new WaitForFixedUpdate();
                var chargeLive = Live(first);
                var blastLive = Live(blast);
                second = Step(launcher, distance, Vector2.right);
                if (chargeLive || blastLive)
                    Assert.IsNull(second, $"step {i}: launched while its own charge or blast was live");
                else
                    Assert.IsNotNull(second, $"step {i}: held after its own charge and blast were gone");
                heldThroughBlast |= !chargeLive && blastLive;
            }

            Assert.IsNotNull(second, "the plan launches again");
            Assert.IsTrue(heldThroughBlast, "the plan held through the blast, not just the charge's flight");
        }

        [UnityTest]
        public IEnumerator Reset_ClearsThePlanState_AndTheOldChargeEndingLeavesTheNewHoldStanding()
        {
            var launcher = MountLauncher();
            var near = launcher.MinLobDistance + 1f;
            var far = launcher.Range - 1f;

            var old = Step(launcher, near, Vector2.right);
            Assert.IsNotNull(old);
            ConcussionWave oldBlast = null;
            old.Spawned += (child, _) => oldBlast = child as ConcussionWave;
            Assert.IsFalse(launcher.ShouldFire(At(far)), "a live charge holds the plan");

            launcher.Reset();
            prevHeld = false;
            Assert.IsTrue(launcher.ShouldFire(At(far)), "Reset forgets the live charge");

            // The opposite way and farther, so it lands after the old blast and clear of it.
            var current = Step(launcher, far, Vector2.left);
            Assert.IsNotNull(current);
            ConcussionWave currentBlast = null;
            current.Spawned += (child, _) => currentBlast = child as ConcussionWave;

            var checkedAfterOldEnded = false;
            for (var i = 0; i < MaxSteps && !checkedAfterOldEnded; i++)
            {
                yield return new WaitForFixedUpdate();
                if (Live(old) || Live(oldBlast) || !oldBlast) continue;
                Assert.IsTrue(Live(current) || Live(currentBlast), "the new charge or its blast is still live");
                Assert.IsFalse(launcher.ShouldFire(At(far)), "the old charge ending leaves the new charge's hold standing");
                checkedAfterOldEnded = true;
            }

            Assert.IsTrue(checkedAfterOldEnded, "the old charge and its blast ended while the new charge flew");
        }
    }
}
#endif
