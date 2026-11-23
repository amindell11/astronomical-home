using System;
using System.Linq;
using System.Reflection;
using Combat.Targeting;
using Damage;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Keystones;
using Ships.Damage;
using Ships.Keystones.Kits;
using Ships.Keystones.Tells;
using Ships.Loadout;
using Ships.Registry;
using Substrate;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Ships
{
    [TestFixture]
    [Category("Ships")]
    public sealed class KeystoneEditModeTests
    {
        private const float Dt = 0.02f;
        private const float Impulse = 2000f;
        private const float Cooldown = 1f;

        private GameObject host;
        private GameObject contact;

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host);
            if (contact)
            {
                if (contact.TryGetComponent<DamageController>(out var damage))
                    DamageableColliders.Unregister(damage);
                Object.DestroyImmediate(contact);
            }
        }

        private const float Window = 0.4f;

        private Dash NewDash() => NewKit<Dash>();

        private T NewKit<T>() where T : Dash
        {
            host = new GameObject(typeof(T).Name);
            var kit = host.AddComponent<T>();
            var so = new SerializedObject(kit);
            so.FindProperty("impulse").floatValue = Impulse;
            so.FindProperty("cooldown").floatValue = Cooldown;
            var window = so.FindProperty("window");
            if (window != null) window.floatValue = Window;
            so.ApplyModifiedPropertiesWithoutUndo();
            return kit;
        }

        private static Kinematics Facing(float yawDeg) => new(Vector2.zero, Vector2.zero, yawDeg, 0f, 0f);

        private static readonly DamageInfo AnyHit = new(10f, DamageKind.Missile, ShipId.Invalid, 0f, Vector3.zero, Vector3.zero);

        [Test]
        public void GuardDash_BlocksEveryHitForItsWindow_ThenNone()
        {
            var guard = NewKit<GuardDash>();
            var kin = Facing(0f);
            Assert.That(guard.Blocks(in AnyHit), Is.False, "nothing is blocked before the first activation");

            guard.Step(in kin, 1f, Dt, out _);
            var steps = 0;
            while (guard.Blocks(in AnyHit))
            {
                guard.Step(in kin, 0f, Dt, out _);
                steps++;
            }
            Assert.That(steps * Dt, Is.EqualTo(Window).Within(Dt), "the guard lasts its window");
            Assert.That(guard.Guarding, Is.False);
        }

        [Test]
        public void GuardDash_ResetClosesAnOpenWindow()
        {
            var guard = NewKit<GuardDash>();
            var kin = Facing(0f);
            guard.Step(in kin, 1f, Dt, out _);
            Assert.That(guard.Guarding, Is.True);

            guard.ResetKeystone();

            Assert.That(guard.Guarding, Is.False, "a respawn never inherits an open window");
            Assert.That(guard.Activations, Is.EqualTo(0));
        }

        [Test]
        public void GuardDash_PaysOnce_ForAHitBlockedInsideItsWindow()
        {
            var guard = NewKit<GuardDash>();
            var kin = Facing(0f);
            guard.NotifyBlocked(AnyHit);
            Assert.That(guard.PaidActivations, Is.EqualTo(0), "a block before any activation pays nothing");

            guard.Step(in kin, 1f, Dt, out _);
            guard.NotifyBlocked(AnyHit);
            guard.NotifyBlocked(AnyHit);
            Assert.That(guard.PaidActivations, Is.EqualTo(1), "one activation pays once");
        }

        [Test]
        public void RamDash_BlocksOnlyCollisions_WhileLive()
        {
            var ram = NewRam();
            var kin = Facing(0f);
            var collision = new DamageInfo(10f, DamageKind.Collision, ShipId.Invalid, 0f, Vector3.zero, Vector3.zero);
            Assert.That(ram.Blocks(in collision), Is.False, "not live before activating");

            ram.Step(in kin, 1f, Dt, out _);
            Assert.That(ram.Braking, Is.True);
            Assert.That(ram.Active, Is.False);
            Assert.That(ram.Blocks(in collision), Is.False, "no protection during the brake");

            while (ram.Braking)
                ram.Step(in kin, 0f, Dt, out _);

            Assert.That(ram.RamLive, Is.True);
            Assert.That(ram.Blocks(in collision), Is.True, "collision damage is dropped while the ram is live");
            Assert.That(ram.Blocks(in AnyHit), Is.False, "a missile still lands on a charging Bulwark");

            while (ram.RamLive)
                ram.Step(in kin, 0f, Dt, out _);
            Assert.That(ram.Blocks(in collision), Is.False, "an unspent ram expires after its window");
        }

        [Test]
        public void RamDash_BrakesToAFullStop_ThenLaunchesAlongTheNoseAtThatMoment()
        {
            var ram = NewRam();
            var vel = new Vector2(12f, -7f);
            var yaw = 0f;
            var brakeSteps = 0;
            var progress = 0f;
            KeystoneEffect launch = default;
            for (var n = 0; n < 20; n++)
            {
                if (brakeSteps == 10) yaw = 90f;
                var kin = new Kinematics(Vector2.zero, vel, yaw, 0f, 0f);
                var e = ram.Step(in kin, n == 0 ? 1f : 0f, Dt, out _);
                if (brakeSteps == 15)
                {
                    launch = e;
                    break;
                }

                Assert.That(e.Impulse, Is.EqualTo(Vector2.zero));
                Assert.That(ram.HoldsThrottle, Is.True);
                Assert.That(ram.HoldProgress, Is.InRange(progress, 1f));
                progress = ram.HoldProgress;
                vel += e.Acceleration * Dt;
                brakeSteps++;
            }

            Assert.That(brakeSteps, Is.EqualTo(15));
            Assert.That(vel.magnitude, Is.LessThan(1e-4f));
            Assert.That(Vector2.Distance(launch.Impulse, Facing(90f).Forward * ram.Impulse), Is.LessThan(1e-3f));
            Assert.That(ram.RamLive, Is.True);
            Assert.That(ram.HoldsThrottle, Is.False);
            Assert.That(ram.HoldProgress, Is.EqualTo(0f));
        }

        private RamDash NewRam()
        {
            var ram = NewKit<RamDash>();
            var so = new SerializedObject(ram);
            so.FindProperty("impulse").floatValue = 13500f;
            so.FindProperty("brake").floatValue = 0.3f;
            so.FindProperty("window").floatValue = 0.6f;
            so.FindProperty("hitCooldown").floatValue = Cooldown;
            so.FindProperty("missDump").floatValue = 0f;
            so.FindProperty("sightAngle").floatValue = 15f;
            so.FindProperty("surgeSpeedScale").floatValue = 1f;
            so.FindProperty("surgeAcceleration").floatValue = 0f;
            so.FindProperty("massScale").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            ram.Initialize(new KeystoneHost(host.AddComponent<Rigidbody>(), 1f, new LockChannel()));
            return ram;
        }

        private static KeystoneContext Sight(Vector2 targetPos, Vector2 targetVel = default, Vector2 selfVel = default) =>
            new(new Kinematics(Vector2.zero, selfVel, 0f, 0f, 0f), 1200f, 21f, new Kinematics(targetPos, targetVel, 0f, 0f, 0f));

        [Test]
        public void RamSight_FiresOnlyWhenAlignedAndInReachAfterTheBrake()
        {
            var ram = NewRam();
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 5f))), Is.True, "on the nose, inside the 6.75 u a launch from rest covers in the window");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(5f, 5f))), Is.False, "45 degrees off the nose");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 5f), targetVel: new Vector2(0f, 10f))), Is.False, "pulling away: 8 u off at launch, closing at 1.25");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 10f))), Is.False, "out of reach from a standstill");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 8f), selfVel: new Vector2(0f, 10f))), Is.True, "speed into the brake carries Bulwark 1.5 u closer");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 5f), selfVel: new Vector2(0f, -6f))), Is.True, "sliding away no longer spoils the ram: the brake stops it first");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 6f), targetVel: new Vector2(0f, 21f), selfVel: new Vector2(0f, 21f))), Is.False,
                "a target fleeing at the cap outruns a launch from rest");
        }

        [Test]
        public void RamSight_BurnCarriesAStandstillRamTheLaunchMisses()
        {
            var ram = NewRam();
            var so = new SerializedObject(ram);
            so.FindProperty("surgeAcceleration").floatValue = 25f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 10f))), Is.True,
                "the burn carries the ram 11.25 u in the window");
        }

        [Test]
        public void RamDash_SurgesHeavyAlongTheLaunchLine_WhileLive()
        {
            var ram = NewRam();
            var so = new SerializedObject(ram);
            so.FindProperty("surgeSpeedScale").floatValue = 1.5f;
            so.FindProperty("surgeAcceleration").floatValue = 25f;
            so.FindProperty("massScale").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var kin = Facing(0f);
            KeystoneEffect launch = default;
            for (var n = 0; n < 40 && !ram.RamLive; n++)
                launch = ram.Step(in kin, n == 0 ? 1f : 0f, Dt, out _);

            Assert.That(ram.RamLive, Is.True);
            Assert.That(Vector2.Distance(launch.Impulse, Facing(0f).Forward * (ram.Impulse * 2f)), Is.LessThan(1e-3f));
            Assert.That(launch.Acceleration, Is.EqualTo(Vector2.zero));
            Assert.That(ram.MassScale, Is.EqualTo(2f));
            Assert.That(ram.SpeedScale, Is.EqualTo(1.5f));
            Assert.That(ram.PeakSpeedScale, Is.EqualTo(1.5f));

            var turned = Facing(90f);
            var alongLaunch = Facing(0f).Forward * 25f;
            var step = ram.Step(in turned, 0f, Dt, out _);
            Assert.That(ram.RamLive, Is.True);
            Assert.That(Vector2.Distance(step.Acceleration, alongLaunch), Is.LessThan(1e-3f));
            while (ram.RamLive)
            {
                step = ram.Step(in turned, 0f, Dt, out _);
                if (ram.RamLive)
                    Assert.That(Vector2.Distance(step.Acceleration, alongLaunch), Is.LessThan(1e-3f));
            }

            Assert.That(ram.MassScale, Is.EqualTo(1f));
            Assert.That(ram.SpeedScale, Is.EqualTo(1f));
            Assert.That(step.Acceleration, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void RamDash_Hit_ShortensTheCooldown_FromThePress()
        {
            var ram = NewRam();
            var so = new SerializedObject(ram);
            so.FindProperty("cooldown").floatValue = 7f;
            so.FindProperty("hitCooldown").floatValue = 3.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            contact = new GameObject("Ram contact");
            var collider = contact.AddComponent<BoxCollider>();

            var kin = Facing(0f);
            ram.Step(in kin, 1f, Dt, out _);
            var steps = 0;
            while (ram.Braking)
            {
                ram.Step(in kin, 0f, Dt, out _);
                steps++;
            }

            var before = ram.CooldownRemaining;
            ram.OnContact(new KeystoneContact(
                ShipId.Invalid, 0f, Vector3.zero, collider, null, Vector3.zero, 1f, 0f));
            Assert.That(ram.CooldownRemaining, Is.LessThanOrEqualTo(before), "contact never lengthens the cooldown");
            Assert.That(ram.CooldownRemaining, Is.EqualTo(3.5f - steps * Dt).Within(Dt),
                "contact cuts the cooldown to hitCooldown from the press");
        }

        [Test]
        public void RamDash_Hit_RecordsTheContact_OncePerRam()
        {
            var ram = NewRam();
            contact = new GameObject("Ram contact");
            var collider = contact.AddComponent<BoxCollider>();
            var kin = Facing(0f);
            ram.Step(in kin, 1f, Dt, out _);
            while (ram.Braking)
                ram.Step(in kin, 0f, Dt, out _);

            var point = new Vector3(3f, 0f, 5f);
            Assert.That(ram.RamLive, Is.True);
            Assert.That(ram.Hits, Is.EqualTo(0));
            ram.OnContact(new KeystoneContact(
                ShipId.Invalid, 0f, Vector3.zero, collider, null, point, 1f, 0f));
            Assert.That(ram.Hits, Is.EqualTo(1));
            Assert.That(ram.LastHitPoint, Is.EqualTo(point));

            ram.OnContact(new KeystoneContact(
                ShipId.Invalid, 0f, Vector3.zero, collider, null, new Vector3(1f, 2f, 4f), 1f, 0f));
            Assert.That(ram.Hits, Is.EqualTo(1), "a second contact in the same ram does not count");
            Assert.That(ram.LastHitPoint, Is.EqualTo(point));
        }

        [Test]
        public void RamDash_Miss_ShedsSurgeSpeed_ThenKeepsTheFullCooldown()
        {
            var ram = NewRam();
            var so = new SerializedObject(ram);
            so.FindProperty("cooldown").floatValue = 7f;
            so.FindProperty("missDump").floatValue = 0.3f;
            so.FindProperty("window").floatValue = 0.45f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var rest = Facing(0f);
            ram.Step(in rest, 1f, Dt, out _, baseMaxSpeed: 21f);
            var steps = 0;
            while (ram.Braking)
            {
                ram.Step(in rest, 0f, Dt, out _, baseMaxSpeed: 21f);
                steps++;
            }

            var vel = new Vector2(30f, 0f);
            var dumpLimit = Mathf.RoundToInt(0.3f / Dt);
            var dumped = 0;
            for (var n = 0; dumped < dumpLimit && n < 80; n++)
            {
                var kin = new Kinematics(Vector2.zero, vel, 0f, 0f, 0f);
                var effect = ram.Step(in kin, 0f, Dt, out _, baseMaxSpeed: 21f);
                steps++;
                if (ram.RamLive)
                    continue;
                vel += effect.Acceleration * Dt;
                dumped++;
                Assert.That(vel.magnitude, Is.GreaterThanOrEqualTo(21f - 1e-3f));
            }

            Assert.That(dumped, Is.EqualTo(dumpLimit), "the miss sheds within its dump");
            Assert.That(vel.magnitude, Is.LessThanOrEqualTo(21f + 1e-3f));
            Assert.That(ram.CooldownRemaining, Is.EqualTo(7f - steps * Dt).Within(Dt),
                "a miss keeps the full cooldown");
        }

        [Test]
        public void RamDash_Brace_HalvesFrontalHits_DuringTheBrakeOnly()
        {
            var ram = NewRam();
            var ahead = From(Vector2.up);
            var beside = From(Vector2.right);
            var behind = From(Vector2.down);
            Assert.That(ram.DamageScale(in ahead), Is.EqualTo(1f), "before activation");

            var kin = Facing(0f);
            ram.Step(in kin, 1f, Dt, out _);
            Assert.That(ram.Braking, Is.True);
            Assert.That(ram.DamageScale(in ahead), Is.EqualTo(0.5f), "ahead of the nose");
            Assert.That(ram.DamageScale(in beside), Is.EqualTo(1f), "beside the ship");
            Assert.That(ram.DamageScale(in behind), Is.EqualTo(1f), "behind the ship");

            while (ram.Braking)
                ram.Step(in kin, 0f, Dt, out _);
            Assert.That(ram.RamLive, Is.True);
            Assert.That(ram.DamageScale(in ahead), Is.EqualTo(1f), "after launch");

            DamageInfo From(Vector2 dir) => new(20f, DamageKind.Laser, ShipId.Invalid, 0f, Vector3.zero,
                ram.Body.worldCenterOfMass + GamePlane.PlaneDirToWorld(dir));
        }

        [Test]
        public void RamDash_Damage_FollowsAim_WithSpeedOnlyAdding()
        {
            var ram = NewRam();
            var so = new SerializedObject(ram);
            so.FindProperty("shipDamage").floatValue = 80f;
            so.FindProperty("damageReferenceSpeed").floatValue = 16f;
            so.FindProperty("maxSpeedBonus").floatValue = 1.6f;
            so.FindProperty("aimFullAngle").floatValue = 30f;
            so.FindProperty("aimFloorAngle").floatValue = 75f;
            so.FindProperty("aimFloor").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            contact = new GameObject("Ram target");
            var collider = contact.AddComponent<BoxCollider>();
            var damage = contact.AddComponent<DamageController>();
            damage.PopulateSettings(new ResolvedShipStats
            {
                maxHealth = 1000f,
                maxShield = 10000f,
                shieldRegenRate = 0f,
                shieldRegenDelay = 1f,
            });
            DamageableColliders.Register(damage, new[] { collider });

            var full = ram.ShipDamage;
            Assert.That(Dealt(Off(0f), 0f), Is.EqualTo(full).Within(1e-3f),
                "below the reference speed, a centred ram still deals a full hit");
            Assert.That(Dealt(Off(0f), 16f), Is.EqualTo(full).Within(1e-3f), "the reference speed is a full hit");
            Assert.That(Dealt(Off(0f), 24f), Is.EqualTo(full * 1.5f).Within(1e-3f), "speed between the reference and the cap adds");
            Assert.That(Dealt(Off(0f), 32f), Is.EqualTo(full * 1.6f).Within(1e-3f), "speed adds only up to the bonus");
            Assert.That(Dealt(Off(20f), 0f), Is.EqualTo(full).Within(1e-3f), "inside the full-aim angle");
            Assert.That(Dealt(Off(52.5f), 0f), Is.EqualTo(full * 0.75f).Within(1e-3f),
                "aim eases from a full hit to the floor");
            Assert.That(Dealt(Off(90f), 0f), Is.EqualTo(full * 0.5f).Within(1e-3f), "past the floor angle the clip stays at the floor");
            Assert.That(Dealt(Off(90f), 32f), Is.EqualTo(full * 0.5f * 1.6f).Within(1e-3f), "speed still adds on a poor aim");
            Assert.That(Dealt(Vector2.zero, 0f), Is.EqualTo(full).Within(1e-3f),
                "a target whose centre sits on the ship counts as centred");

            float Dealt(Vector2 planeOffset, float relativeSpeed)
            {
                contact.transform.position = ram.Body.worldCenterOfMass + GamePlane.PlaneDirToWorld(planeOffset);
                Physics.SyncTransforms();
                ram.ResetKeystone();
                var kin = Facing(0f);
                ram.Step(in kin, 1f, Dt, out _);
                while (ram.Braking)
                    ram.Step(in kin, 0f, Dt, out _);
                damage.ResetDamageState();
                var before = damage.Shield.CurrentValue + damage.Health.CurrentValue;
                ram.OnContact(new KeystoneContact(
                    ShipId.Invalid, 1f, Vector3.zero, collider, null, Vector3.zero, 0f, relativeSpeed));
                return before - (damage.Shield.CurrentValue + damage.Health.CurrentValue);
            }

            static Vector2 Off(float angleDeg)
            {
                var rad = angleDeg * Mathf.Deg2Rad;
                return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 8f;
            }
        }

        [Test]
        public void OtherKits_HaveNoSight()
        {
            Assert.That(NewDash().ShouldActivate(Sight(new Vector2(0f, 2f))), Is.False);
            Assert.That(NewKit<GuardDash>().ShouldActivate(Sight(new Vector2(0f, 2f))), Is.False);
        }

        [Test]
        public void PlainDash_BlocksNothing_EvenRightAfterActivating()
        {
            var dash = NewDash();
            var kin = Facing(0f);
            dash.Step(in kin, 1f, Dt, out _);
            Assert.That(dash.Blocks(in AnyHit), Is.False);
        }

        [Test]
        public void Dash_ActivatesOnce_AlongTheNose_ThenWaitsOutItsCooldown()
        {
            var dash = NewDash();
            var kin = Facing(90f);

            var impulse = dash.Step(in kin, 1f, Dt, out var activated).Impulse;
            Assert.That(activated, Is.True);
            Assert.That(Vector2.Distance(impulse, kin.Forward * Impulse), Is.LessThan(1e-3f));
            Assert.That(dash.Ready, Is.False);
            Assert.That(dash.CooldownPct, Is.EqualTo(1f).Within(1e-5f));

            var steps = 0;
            do
            {
                impulse = dash.Step(in kin, 1f, Dt, out activated).Impulse;
                if (!activated) Assert.That(impulse, Is.EqualTo(Vector2.zero), "held input does not re-fire inside the cooldown");
                steps++;
            } while (!activated && steps < 1000);
            Assert.That(activated, Is.True, "held input re-fires once the cooldown has elapsed");
            Assert.That(steps * Dt, Is.EqualTo(Cooldown).Within(Dt));
        }

        [Test]
        public void Dash_ZeroInput_NeverActivatesOrStartsTheCooldown()
        {
            var dash = NewDash();
            var kin = Facing(0f);
            for (var i = 0; i < 10; i++)
            {
                Assert.That(dash.Step(in kin, 0f, Dt, out var activated).Impulse, Is.EqualTo(Vector2.zero));
                Assert.That(activated, Is.False);
            }
            Assert.That(dash.Ready, Is.True);
        }

        [Test]
        public void ResetKeystone_MakesACoolingKitReady()
        {
            var dash = NewDash();
            var kin = Facing(0f);
            dash.Step(in kin, 1f, Dt, out _);
            Assert.That(dash.Ready, Is.False);

            dash.ResetKeystone();

            Assert.That(dash.Ready, Is.True);
            Assert.That(dash.CooldownRemaining, Is.EqualTo(0f));
        }

        [Test]
        public void Ship_WithNoKeystone_FailsAtInitialize()
        {
            host = new GameObject("Keystoneless Ship");
            var ship = host.AddComponent<Ship>();

            var thrown = Assert.Throws<InvalidOperationException>(() => ship.Initialize(0, 0, null));
            StringAssert.Contains("keystone", thrown.Message);
        }

        [Test]
        public void Bulwark_CarriesTheRam()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var ram = catalog.Chassis.Single(c => c.name == "Bulwark").KeystonePrefab as RamDash;
            Assert.That(ram, Is.Not.Null, "Bulwark's kit is the ram");
            Assert.That(new SerializedObject(ram).FindProperty("brake").floatValue, Is.EqualTo(0.4f));
            Assert.That(ram.Window, Is.EqualTo(0.45f));
            Assert.That(ram.ShipDamage, Is.EqualTo(80f));
            Assert.That(ram.GetComponent<KeystoneFlash>(), Is.Not.Null, "the kit carries its tell");
        }

        [Test]
        public void Valis_CarriesOverdrive()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var overdrive = catalog.Chassis.Single(c => c.name == "Valis").KeystonePrefab as Overdrive;
            Assert.That(overdrive, Is.Not.Null, "Valis's kit is Overdrive");
            Assert.That(overdrive.name, Is.EqualTo("Valis_Overdrive"));
            Assert.That(overdrive.Cooldown, Is.EqualTo(9f));
            Assert.That(overdrive.Window, Is.EqualTo(5f));
            Assert.That(overdrive.PeakSpeedScale, Is.EqualTo(1.5f));
            Assert.That(overdrive.PeakThrustScale, Is.EqualTo(1.5f));
            Assert.That(overdrive.GetComponent<KeystoneFlipbook>(), Is.Not.Null, "the kit carries its overdrive flipbook");
            Assert.That(overdrive.GetComponent<KeystoneGhosts>(), Is.Not.Null, "the kit carries its hull ghosts");
            Assert.That(overdrive.GetComponent<KeystoneFlash>(), Is.Null, "the kit leaves the hull untouched");
        }

        private Overdrive NewOverdrive()
        {
            host = new GameObject("Overdrive");
            var kit = host.AddComponent<Overdrive>();
            var so = new SerializedObject(kit);
            so.FindProperty("cooldown").floatValue = 8f;
            so.FindProperty("window").floatValue = 3f;
            so.FindProperty("speedScale").floatValue = 1.35f;
            so.FindProperty("thrustScale").floatValue = 1.5f;
            so.FindProperty("maneuverabilityScale").floatValue = 1.5f;
            so.FindProperty("sightSaturation").floatValue = 0.8f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return kit;
        }

        [Test]
        public void Overdrive_ScalesForItsWindow_ThenReleases_AndPaysOnlyAboveTheBaseCap()
        {
            var kit = NewOverdrive();
            var slow = new Kinematics(Vector2.zero, new Vector2(0f, 20f), 0f, 0f, 0f);
            var fast = new Kinematics(Vector2.zero, new Vector2(0f, 35f), 0f, 0f, 0f);
            Assert.That(kit.SpeedScale, Is.EqualTo(1f));
            Assert.That(kit.ManeuverabilityScale, Is.EqualTo(1f));

            Assert.That(kit.Step(in slow, 1f, Dt, out var activated, 32f).Impulse, Is.EqualTo(Vector2.zero), "no impulse");
            Assert.That(activated, Is.True);
            Assert.That(kit.SpeedScale, Is.EqualTo(1.35f));
            Assert.That(kit.ThrustScale, Is.EqualTo(1.5f));
            Assert.That(kit.ManeuverabilityScale, Is.EqualTo(1.5f));
            Assert.That(kit.PaidActivations, Is.EqualTo(0), "under the base cap pays nothing");

            kit.Step(in fast, 0f, Dt, out _, 32f);
            Assert.That(kit.PaidActivations, Is.EqualTo(1), "flying past the base cap pays");

            while (kit.Active) kit.Step(in slow, 0f, Dt, out _, 32f);
            Assert.That(kit.SpeedScale, Is.EqualTo(1f));
            Assert.That(kit.ThrustScale, Is.EqualTo(1f));
            Assert.That(kit.ManeuverabilityScale, Is.EqualTo(1f));
        }

        [Test]
        public void OverdriveSight_FiresOnlyWhenSaturated()
        {
            var kit = NewOverdrive();
            KeystoneContext Ctx(float speed, Vector2 desired) =>
                new(new Kinematics(Vector2.zero, new Vector2(0f, speed), 0f, 0f, 0f), 650f, 32f, default, desired);
            Assert.That(kit.ShouldActivate(Ctx(27f, new Vector2(0f, 27f))), Is.True, "cruising at 85% of the cap and asking to stay there");
            Assert.That(kit.ShouldActivate(Ctx(5f, new Vector2(0f, 32f))), Is.True, "asking for the cap from low speed");
            Assert.That(kit.ShouldActivate(Ctx(31f, new Vector2(0f, 15f))), Is.False, "asking for well under the cap");
            Assert.That(kit.ShouldActivate(Ctx(31f, new Vector2(float.NaN, float.NaN))), Is.False, "no velocity request");
        }

        [Test]
        public void Crimson_CarriesBlink()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var blink = catalog.Chassis.Single(c => c.name == "Crimson").KeystonePrefab as Blink;
            Assert.That(blink, Is.Not.Null, "Crimson's kit is Blink");
            Assert.That(blink.name, Is.EqualTo("Crimson_Blink"));
            Assert.That(blink.Range, Is.EqualTo(12f));
            Assert.That(blink.Cooldown, Is.EqualTo(4f));
            Assert.That(blink.GetComponent<KeystoneEmergence>(), Is.Not.Null, "the kit carries its tell");
            Assert.That(blink.GetComponent<KeystoneFlipbook>(), Is.Not.Null, "the kit carries its blink flipbook");
            Assert.That(blink.GetComponent<KeystoneFlash>(), Is.Null, "the kit carries no flash");
        }

        [Test]
        public void Nightshade_CarriesStealth()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var stealth = catalog.Chassis.Single(c => c.name == "Nightshade").KeystonePrefab as Stealth;
            Assert.That(stealth, Is.Not.Null, "Nightshade's kit is Stealth");
            Assert.That(stealth.name, Is.EqualTo("Nightshade_Stealth"));
            Assert.That(stealth.Window, Is.EqualTo(2.5f));
            Assert.That(stealth.Cooldown, Is.EqualTo(12f));
            Assert.That(stealth.PeakSpeedScale, Is.EqualTo(1.3f));
            Assert.That(stealth.PeakThrustScale, Is.EqualTo(1.45f));
            Assert.That(stealth.GetComponent<StealthVeil>(), Is.Not.Null, "the kit carries its tell");
        }

        [Test]
        public void TargetLock_DropsAConcealedTarget_InTheSameCall()
        {
            host = new GameObject("Locked Ship");
            var target = host.AddComponent<Ship>();
            var released = 0;
            target.Lock.Released += () => released++;
            var targetLock = new Combat.Targeting.TargetLock(0.5f, 3f, () => true);
            Assert.That(targetLock.TryStartLock(target), Is.True);

            target.Lock.Conceal();

            Assert.That(targetLock.State, Is.EqualTo(Combat.Targeting.LockState.Idle), "the lock drops the moment its target conceals");
            Assert.That(targetLock.CurrentTarget, Is.Null);
            Assert.That(released, Is.EqualTo(1), "the lock indicator hears the release");
            Assert.That(targetLock.TryStartLock(target), Is.False, "a concealed ship cannot be locked again");

            target.Lock.Reveal();
            Assert.That(targetLock.TryStartLock(target), Is.True, "and can be once revealed");
        }

        [Test]
        public void Vanguard_CarriesTheGuardDash()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var guard = catalog.Chassis.Single(c => c.name == "Vanguard").KeystonePrefab as GuardDash;
            Assert.That(guard, Is.Not.Null, "Vanguard's kit is the Guard Dash");
            Assert.That(guard.DisplayName, Is.EqualTo("Dash"));
            Assert.That(guard.Window, Is.EqualTo(0.4f));
            Assert.That(guard.GetComponent<KeystoneFlash>(), Is.Not.Null, "the kit carries its tell");
            Assert.That(guard.GetComponent<KeystoneFlipbook>(), Is.Not.Null, "the kit carries its dash flipbook");
        }

        [Test]
        public void Bulwark_CarriesItsRamFlipbook()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var ram = catalog.Chassis.Single(c => c.name == "Bulwark").KeystonePrefab as RamDash;
            Assert.That(ram, Is.Not.Null, "Bulwark's kit is the ram");
            Assert.That(ram.GetComponent<KeystoneFlipbook>(), Is.Not.Null, "the kit carries its ram flipbook");
        }

        [Test]
        public void StartsBehind_WithoutStayingAtLaunch_ThrowsOnAwake()
        {
            var feel = ScriptableObject.CreateInstance<KeystoneFlipbookFeel>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Visuals/Vfx/Keystones/VanguardDash/VanguardDash_Under.mat");
            host = new GameObject("Flipbook");
            host.SetActive(false);
            try
            {
                Assert.That(material, Is.Not.Null, "a sheet material to build the layer");
                var feelObject = new SerializedObject(feel);
                var layers = feelObject.FindProperty("layers");
                layers.arraySize = 1;
                var layer = layers.GetArrayElementAtIndex(0);
                layer.FindPropertyRelative("material").objectReferenceValue = material;
                layer.FindPropertyRelative("columns").intValue = 1;
                layer.FindPropertyRelative("rows").intValue = 1;
                layer.FindPropertyRelative("frames").intValue = 1;
                layer.FindPropertyRelative("startsBehind").boolValue = true;
                layer.FindPropertyRelative("staysAtLaunch").boolValue = false;
                feelObject.ApplyModifiedPropertiesWithoutUndo();

                var kit = host.AddComponent<Blink>();
                var flipbook = host.AddComponent<KeystoneFlipbook>();
                var part = new SerializedObject(flipbook);
                part.FindProperty("keystone").objectReferenceValue = kit;
                part.FindProperty("shipLength").floatValue = 1f;
                part.FindProperty("feel").objectReferenceValue = feel;
                part.ApplyModifiedPropertiesWithoutUndo();

                // Edit mode never wakes a part, so the test calls Awake itself.
                var awake = typeof(KeystoneFlipbook).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                var thrown = Assert.Throws<TargetInvocationException>(() => awake.Invoke(flipbook, null));
                Assert.That(thrown.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(thrown.InnerException.Message, Does.Contain("starts behind only when it stays at launch"));
            }
            finally
            {
                Object.DestroyImmediate(feel);
            }
        }

        [Test]
        public void Loops_WithoutEndingWithLive_ThrowsOnAwake()
        {
            var error = FlipbookAwakeError(layer =>
            {
                layer.FindPropertyRelative("frames").intValue = 4;
                layer.FindPropertyRelative("loops").boolValue = true;
                layer.FindPropertyRelative("endsWithLive").boolValue = false;
            });
            Assert.That(error.Message, Does.Contain("loops only when it ends with live"));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void LoopFrom_OutsideTheSheetsFrames_ThrowsOnAwake(int loopFrom)
        {
            var error = FlipbookAwakeError(layer =>
            {
                layer.FindPropertyRelative("frames").intValue = 4;
                layer.FindPropertyRelative("loops").boolValue = true;
                layer.FindPropertyRelative("endsWithLive").boolValue = true;
                layer.FindPropertyRelative("loopFrom").intValue = loopFrom;
            });
            Assert.That(error.Message, Does.Contain("needs loopFrom from 0 to 3"));
        }

        private InvalidOperationException FlipbookAwakeError(Action<SerializedProperty> authorLayer)
        {
            var feel = ScriptableObject.CreateInstance<KeystoneFlipbookFeel>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Visuals/Vfx/Keystones/VanguardDash/VanguardDash_Under.mat");
            host = new GameObject("Flipbook");
            host.SetActive(false);
            try
            {
                Assert.That(material, Is.Not.Null, "a sheet material to build the layer");
                var feelObject = new SerializedObject(feel);
                var layers = feelObject.FindProperty("layers");
                layers.arraySize = 1;
                var layer = layers.GetArrayElementAtIndex(0);
                layer.FindPropertyRelative("material").objectReferenceValue = material;
                layer.FindPropertyRelative("columns").intValue = 2;
                layer.FindPropertyRelative("rows").intValue = 2;
                authorLayer(layer);
                feelObject.ApplyModifiedPropertiesWithoutUndo();

                var kit = host.AddComponent<Blink>();
                var flipbook = host.AddComponent<KeystoneFlipbook>();
                var part = new SerializedObject(flipbook);
                part.FindProperty("keystone").objectReferenceValue = kit;
                part.FindProperty("shipLength").floatValue = 1f;
                part.FindProperty("feel").objectReferenceValue = feel;
                part.ApplyModifiedPropertiesWithoutUndo();

                // Edit mode never wakes a part, so the test calls Awake itself.
                var awake = typeof(KeystoneFlipbook).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                var thrown = Assert.Throws<TargetInvocationException>(() => awake.Invoke(flipbook, null));
                Assert.That(thrown.InnerException, Is.TypeOf<InvalidOperationException>());
                return (InvalidOperationException)thrown.InnerException;
            }
            finally
            {
                Object.DestroyImmediate(feel);
            }
        }

        [TestCase("Vanguard", 9000f, 5f)]
        [TestCase("Bulwark", 19500f, 7f)]
        [TestCase("Junker_1", 15000f, 5f)]
        public void Chassis_CarriesItsOwnDash(string chassis, float impulse, float cooldown)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var ship = catalog.Chassis.Single(c => c.name == chassis);
            var dash = ship.KeystonePrefab as Dash;
            Assert.That(dash, Is.Not.Null, $"{chassis}: the keystone is a Dash");
            Assert.That(dash.name, Is.EqualTo($"{chassis.Split('_')[0]}_Dash"), $"{chassis}: its own kit prefab");
            Assert.That(dash.Impulse, Is.EqualTo(impulse), $"{chassis}: impulse");
            Assert.That(dash.Cooldown, Is.EqualTo(cooldown), $"{chassis}: cooldown");
        }

        [Test, Category("RequiresLfsPng")]
        public void Kits_AuthorTheirHudIcons()
        {
            AssertIcon("Vanguard_Dash", "Assets/Visuals/Ui/Keystones/Generated/dash.png");
            AssertIcon("Bulwark_Dash", "Assets/Visuals/Ui/Keystones/Generated/ram.png");
            AssertIcon("Valis_Overdrive", "Assets/Visuals/Ui/Keystones/Generated/overdrive.png");
            AssertIcon("Crimson_Blink", "Assets/Visuals/Ui/Keystones/Generated/blink.png");
            AssertIcon("Nightshade_Stealth", "Assets/Visuals/Ui/Keystones/Generated/stealth.png");
            AssertIcon("Junker_Dash", "Assets/Visuals/Ui/Crosshair/Generated/keystone-placeholder.png");
            Assert.That(AssetDatabase.AssetPathToGUID(
                    "Assets/Visuals/Ui/Crosshair/Generated/keystone-placeholder.png"),
                Is.EqualTo("9f5919c47706b7f438d1531e6e55719d"));
        }

        [Test]
        public void ActivateOnArrival_ConcealsStealthAtOnce_AndThrowsWhenNotReady()
        {
            var channel = new LockChannel();
            var kit = NewStealth();
            kit.Initialize(new KeystoneHost(host.GetComponent<Rigidbody>(), 1f, channel));
            var kin = Facing(0f);

            kit.ActivateOnArrival(in kin);

            Assert.That(kit.Active, Is.True);
            Assert.That(channel.IsConcealed, Is.True);
            Assert.That(kit.Ready, Is.False);
            Assert.That(kit.CooldownRemaining, Is.EqualTo(kit.Cooldown).Within(1e-4f));
            Assert.Throws<InvalidOperationException>(() => kit.ActivateOnArrival(in kin));
        }

        [Test]
        public void ActivateOnArrival_ThrowsWhenTheActivationMovesTheShip()
        {
            var dash = NewDash();
            var kin = Facing(0f);
            Assert.Throws<InvalidOperationException>(() => dash.ActivateOnArrival(in kin));
        }

        [Test]
        public void Telling_TurnsOnAtTheLead_AndOffWhenTheWindowEndsOrTheShipFires()
        {
            var kin = Facing(0f);
            var kit = NewStealth(window: 1f, tellLead: 0.4f);
            kit.ActivateOnArrival(in kin);
            Assert.That(kit.Telling, Is.False, "the tell waits for the lead");

            kit.Step(in kin, 0f, 0.5f, out _);
            Assert.That(kit.Telling, Is.False, "still short of window - tellLead");
            kit.Step(in kin, 0f, 0.2f, out _);
            Assert.That(kit.Telling, Is.True, "the tell starts at window - tellLead");

            kit.NotifyFired();
            Assert.That(kit.Telling, Is.False, "firing ends the tell");

            kit.ResetKeystone();
            kit.ActivateOnArrival(in kin);
            kit.Step(in kin, 0f, 0.7f, out _);
            Assert.That(kit.Telling, Is.True);
            kit.Step(in kin, 0f, 0.4f, out _);
            Assert.That(kit.Active, Is.False);
            Assert.That(kit.Telling, Is.False, "the window ending ends the tell");
        }

        private Stealth NewStealth(float window = 1f, float tellLead = 0.4f)
        {
            host = new GameObject("Stealth");
            var body = host.AddComponent<Rigidbody>();
            var kit = host.AddComponent<Stealth>();
            var so = new SerializedObject(kit);
            so.FindProperty("cooldown").floatValue = Cooldown;
            so.FindProperty("window").floatValue = window;
            so.FindProperty("tellLead").floatValue = tellLead;
            so.ApplyModifiedPropertiesWithoutUndo();
            kit.Initialize(new KeystoneHost(body, 1f, new LockChannel()));
            return kit;
        }

        private static void AssertIcon(string prefab, string spritePath)
        {
            var kit = AssetDatabase.LoadAssetAtPath<Keystone>($"Assets/Prefabs/Keystones/{prefab}.prefab");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            Assert.That(kit, Is.Not.Null, prefab);
            Assert.That(sprite, Is.Not.Null, spritePath);
            Assert.That(kit.Icon, Is.SameAs(sprite), prefab);
        }
    }
}
