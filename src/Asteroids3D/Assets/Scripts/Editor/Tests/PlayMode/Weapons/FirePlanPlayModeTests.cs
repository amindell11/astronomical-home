#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
    /// Each weapon's fire plan over its authored prefab, stepped the way the Gunner steps it: the plan
    /// decides the trigger, then the weapon handles that trigger.
    /// </summary>
    [Category("Weapons")]
    public class FirePlanPlayModeTests : PlayModeWorldFixture
    {
        private const string ChargeLasersPrefabPath = "Assets/Prefabs/Weapons/ChargeLasers.prefab";
        private const string RailgunPrefabPath = "Assets/Prefabs/Weapons/Railgun.prefab";
        private const string LasersPrefabPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const string TurboLasersPrefabPath = "Assets/Prefabs/Weapons/TurboLasers.prefab";
        private const int MaxSteps = 1000;

        protected override bool AccelerateTime => true;

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        private T InstantiateWeapon<T>(string path) where T : WeaponComponent
        {
            var prefab = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(prefab, $"Failed to load weapon prefab at {path}");
            var weapon = Object.Instantiate(prefab, Vector3.zero, GamePlane.Rotation);
            spawned.Add(weapon.gameObject);
            return weapon;
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float GetFloat(Object target, string field) =>
            new SerializedObject(target).FindProperty(field).floatValue;

        private static TargetingContext Target(float angle, float distance = 10f) => new()
        {
            distanceToTarget = distance,
            angleToTarget = angle,
            hasLineOfSight = true,
        };

        private bool Step(WeaponComponent weapon, TargetingContext context)
        {
            var held = weapon.HoldTrigger(context);
            var targetPoint = weapon.transform.position + weapon.transform.up * context.distanceToTarget;
            weapon.HandleTrigger(new WeaponCommand { held = held, targetPoint = targetPoint }, Projectiles);
            return held;
        }

        private static float ChargeStep(ChargeTime charge) => Time.fixedDeltaTime / charge.FullChargeTime;

        [UnityTest]
        public IEnumerator ChargeLasers_OnSolution_ReleaseAtTheReleaseCharge_AndWaitOutTheCooldown()
        {
            var laser = InstantiateWeapon<ChargeLasers>(ChargeLasersPrefabPath);
            // Longer than the charge to the release point, so the second release must wait for it.
            SetFloat(laser.GetComponent<Cooldown>(), "fireRate", 1f);
            var releaseCharge = GetFloat(laser, "releaseCharge");
            var spent = new List<float>();
            laser.Charge.OnChargeSpent += spent.Add;

            var wasted = 0;
            var held = false;
            var steps = Mathf.CeilToInt(2.5f / Time.fixedDeltaTime);
            for (var step = 0; step < steps; step++)
            {
                var shots = spent.Count;
                var wasHeld = held;
                held = Step(laser, Target(0f));
                if (wasHeld && !held && spent.Count == shots) wasted++;
                yield return new WaitForFixedUpdate();
            }

            Assert.GreaterOrEqual(spent.Count, 2, "On solution the plan fires again once the cooldown ends.");
            Assert.AreEqual(0, wasted, "Every release fired: none came while the cooldown ran.");
            Assert.That(spent[0], Is.GreaterThanOrEqualTo(releaseCharge).And.LessThan(releaseCharge + ChargeStep(laser.Charge)),
                "The first shot is released on the first step at the release charge.");
            Assert.Greater(spent[1], releaseCharge + ChargeStep(laser.Charge),
                "The second shot kept charging through the cooldown instead of dropping the charge.");
        }

        [Test]
        public void ChargeLasers_ReleaseChargeUnderTheMinimum_ReleaseWaitsForTheMinimum()
        {
            var laser = InstantiateWeapon<ChargeLasers>(ChargeLasersPrefabPath);
            laser.Charge.Configure(chargeTime: Time.fixedDeltaTime * 20f, minChargeToFire: 0.5f);
            SetFloat(laser, "releaseCharge", 0.1f);
            float? spent = null;
            laser.Charge.OnChargeSpent += charge => spent = charge;

            for (var i = 0; i < 40 && spent == null; i++)
                Step(laser, Target(0f));

            Assert.IsNotNull(spent, "The plan fires once the charge reaches its minimum.");
            Assert.GreaterOrEqual(spent.Value, 0.5f - ChargeStep(laser.Charge) * 0.5f,
                "No release below the minimum, where it would drop the charge.");
        }

        private void ChargeToTheCommitPoint(Railguns railgun)
        {
            var commitPoint = GetFloat(railgun, "commitPoint");
            Assert.IsTrue(Step(railgun, Target(5f)), "Within the start angle, the plan starts a charge.");
            for (var i = 0; i < MaxSteps && railgun.Charge.ChargePct < commitPoint; i++)
                Assert.IsTrue(Step(railgun, Target(25f)), "Before the commit point, the plan keeps charging within the break angle.");
        }

        [Test]
        public void Railgun_StartsOnlyWithinTheStartAngle_AndKeepsChargingWithinTheBreakAngle()
        {
            var railgun = InstantiateWeapon<Railguns>(RailgunPrefabPath);

            Assert.IsFalse(railgun.HoldTrigger(Target(15f)), "Outside the start angle, no charge starts.");
            ChargeToTheCommitPoint(railgun);
            Assert.GreaterOrEqual(railgun.Charge.ChargePct, GetFloat(railgun, "commitPoint"));
        }

        [Test]
        public void Railgun_BeforeTheCommitPoint_DropsPastTheBreakAngle()
        {
            var railgun = InstantiateWeapon<Railguns>(RailgunPrefabPath);

            Assert.IsTrue(Step(railgun, Target(5f)));
            Assert.IsFalse(Step(railgun, Target(35f)), "Past the break angle, the plan releases.");
            Assert.AreEqual(0f, railgun.Charge.ChargePct, "The release drops the charge.");
        }

        [Test]
        public void Railgun_PastTheCommitPoint_FiresWhateverTheAngle()
        {
            var railgun = InstantiateWeapon<Railguns>(RailgunPrefabPath);
            var shots = 0;
            railgun.OnFire += () => shots++;

            ChargeToTheCommitPoint(railgun);
            for (var i = 0; i < MaxSteps && shots == 0; i++)
                Assert.IsTrue(Step(railgun, Target(45f)), "Past the commit point, the plan holds whatever the angle.");
            Assert.AreEqual(1, shots, "The committed charge fires at full.");
        }

        [Test]
        public void Railgun_LeavingRange_DropsTheShotEvenPastTheCommitPoint()
        {
            var railgun = InstantiateWeapon<Railguns>(RailgunPrefabPath);
            var shots = 0;
            railgun.OnFire += () => shots++;

            ChargeToTheCommitPoint(railgun);
            Assert.IsFalse(Step(railgun, Target(0f, GetFloat(railgun, "fireDistance") + 1f)), "Out of range, the plan releases.");
            Assert.AreEqual(0, shots);
            Assert.AreEqual(0f, railgun.Charge.ChargePct, "The release drops the charge.");
        }

        [UnityTest]
        public IEnumerator Railgun_OnSolution_BuildsNoChargeUntilTheCooldownEnds()
        {
            var railgun = InstantiateWeapon<Railguns>(RailgunPrefabPath);
            var cooldown = railgun.GetComponent<Cooldown>();
            var shots = 0;
            railgun.OnFire += () => shots++;

            for (var i = 0; i < MaxSteps && shots == 0; i++)
            {
                Step(railgun, Target(0f));
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(1, shots, "On solution the plan charges and fires.");

            var recoverySteps = 0;
            for (; recoverySteps < MaxSteps && !cooldown.CanFire(); recoverySteps++)
            {
                Assert.IsTrue(Step(railgun, Target(0f)), "On solution the plan holds the trigger through the cooldown.");
                Assert.AreEqual(0f, railgun.Charge.ChargePct, "No charge builds while the cooldown runs.");
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(recoverySteps, 0, "The shot started the cooldown.");

            Step(railgun, Target(0f));
            Assert.Greater(railgun.Charge.ChargePct, 0f, "The charge starts once the cooldown ends.");
        }

        [TestCase(TurboLasersPrefabPath, true, TestName = "TurboLasers_VentToColdBeforeFiringAgain")]
        [TestCase(LasersPrefabPath, false, TestName = "Lasers_FireAgainOnceTheNextShotFits")]
        public void HeatWeapon_AtTheOverheatBrink_HoldsFireUntilItsVentPoint(string path, bool ventsToCold)
        {
            var lasers = InstantiateWeapon<Lasers>(path);
            var heat = lasers.Heat;
            for (var i = 0; i < MaxSteps && !heat.WouldOverheatOnNextShot(); i++)
                heat.ProcessFire();
            Assert.IsFalse(lasers.HoldTrigger(Target(0f)), "The next shot would overheat.");

            for (var i = 0; i < MaxSteps && heat.WouldOverheatOnNextShot(); i++)
                heat.Tick(Time.fixedDeltaTime);
            Assert.Greater(heat.HeatPct, 0f);
            Assert.AreEqual(!ventsToCold, lasers.HoldTrigger(Target(0f)), "Below the brink, still warm.");

            for (var i = 0; i < MaxSteps && heat.HeatPct > 0f; i++)
                heat.Tick(Time.fixedDeltaTime);
            Assert.IsTrue(lasers.HoldTrigger(Target(0f)), "Cold, the plan fires again.");
        }

        [Test]
        public void Reset_ClearsPlanState()
        {
            var railgun = InstantiateWeapon<Railguns>(RailgunPrefabPath);
            Step(railgun, Target(5f));
            Assert.IsTrue(railgun.HoldTrigger(Target(25f)), "A started railgun keeps charging outside the start angle.");
            railgun.Reset();
            Assert.IsFalse(railgun.HoldTrigger(Target(25f)), "Reset drops the railgun's charge.");

            var laser = InstantiateWeapon<ChargeLasers>(ChargeLasersPrefabPath);
            Step(laser, Target(20f));
            Assert.IsTrue(laser.HoldTrigger(Target(40f)), "A started charge keeps charging outside the charge cone.");
            laser.Reset();
            Assert.IsFalse(laser.HoldTrigger(Target(40f)), "Reset drops the started charge.");

            var turbo = InstantiateWeapon<Lasers>(TurboLasersPrefabPath);
            for (var i = 0; i < MaxSteps && !turbo.Heat.WouldOverheatOnNextShot(); i++)
                turbo.Heat.ProcessFire();
            Assert.IsFalse(turbo.HoldTrigger(Target(0f)), "Venting.");
            turbo.Reset();
            Assert.IsTrue(turbo.HoldTrigger(Target(0f)), "Reset ends the vent.");
        }
    }
}
#endif
