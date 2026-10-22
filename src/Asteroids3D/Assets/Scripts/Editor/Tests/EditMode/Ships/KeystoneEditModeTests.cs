using System;
using System.Linq;
using Damage;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Keystones;
using Ships.Loadout;
using Ships.Registry;
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

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host);
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
            var ram = NewKit<RamDash>();
            var kin = Facing(0f);
            var collision = new DamageInfo(10f, DamageKind.Collision, ShipId.Invalid, 0f, Vector3.zero, Vector3.zero);
            Assert.That(ram.Blocks(in collision), Is.False, "not live before activating");

            ram.Step(in kin, 1f, Dt, out _);
            Assert.That(ram.Active, Is.True);
            Assert.That(ram.Blocks(in collision), Is.True, "collision damage is dropped while the ram is live");
            Assert.That(ram.Blocks(in AnyHit), Is.False, "a missile still lands on a charging Bulwark");

            while (ram.RamLive)
                ram.Step(in kin, 0f, Dt, out _);
            Assert.That(ram.Blocks(in collision), Is.False, "an unspent ram expires after its window");
        }

        private RamDash NewRam()
        {
            var ram = NewKit<RamDash>();
            var so = new SerializedObject(ram);
            so.FindProperty("impulse").floatValue = 13500f;
            so.FindProperty("window").floatValue = 0.6f;
            so.FindProperty("sightAngle").floatValue = 15f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ram;
        }

        private static KeystoneContext Sight(Vector2 targetPos, Vector2 targetVel = default, Vector2 selfVel = default) =>
            new(new Kinematics(Vector2.zero, selfVel, 0f, 0f, 0f), 1200f, 21f, new Kinematics(targetPos, targetVel, 0f, 0f, 0f));

        [Test]
        public void RamSight_FiresOnlyWhenAlignedNotOpeningAndInReach()
        {
            var ram = NewRam();
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 5f))), Is.True, "on the nose, still, inside the 6.75 u a dash covers in the window");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(5f, 5f))), Is.False, "45 degrees off the nose");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 5f), targetVel: new Vector2(0f, 10f))), Is.False, "pulling away");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 10f))), Is.False, "out of reach from a standstill");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 10f), selfVel: new Vector2(0f, 10f))), Is.True, "closing speed extends the reach");
            Assert.That(ram.ShouldActivate(Sight(new Vector2(0f, 6f), targetVel: new Vector2(0f, 21f), selfVel: new Vector2(0f, 21f))), Is.False,
                "at the speed cap the dash adds no closing speed, so a matched-speed target ahead is out of reach");
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

            var impulse = dash.Step(in kin, 1f, Dt, out var activated);
            Assert.That(activated, Is.True);
            Assert.That(Vector2.Distance(impulse, kin.Forward * Impulse), Is.LessThan(1e-3f));
            Assert.That(dash.Ready, Is.False);
            Assert.That(dash.CooldownPct, Is.EqualTo(1f).Within(1e-5f));

            var steps = 0;
            do
            {
                impulse = dash.Step(in kin, 1f, Dt, out activated);
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
                Assert.That(dash.Step(in kin, 0f, Dt, out var activated), Is.EqualTo(Vector2.zero));
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
            Assert.That(ram.Window, Is.EqualTo(0.6f));
            Assert.That(ram.ShipDamage, Is.EqualTo(60f));
            Assert.That(ram.GetComponent<KeystoneFlash>(), Is.Not.Null, "the kit carries its tell");
        }

        [Test]
        public void Valis_CarriesOverdrive()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var overdrive = catalog.Chassis.Single(c => c.name == "Valis").KeystonePrefab as Overdrive;
            Assert.That(overdrive, Is.Not.Null, "Valis's kit is Overdrive");
            Assert.That(overdrive.name, Is.EqualTo("Valis_Overdrive"));
            Assert.That(overdrive.Cooldown, Is.EqualTo(8f));
            Assert.That(overdrive.Window, Is.EqualTo(3f));
            Assert.That(overdrive.PeakSpeedScale, Is.EqualTo(1.35f));
            Assert.That(overdrive.PeakThrustScale, Is.EqualTo(1.5f));
            Assert.That(overdrive.GetComponent<KeystoneFlash>(), Is.Not.Null, "the kit carries its tell");
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

            Assert.That(kit.Step(in slow, 1f, Dt, out var activated, 32f), Is.EqualTo(Vector2.zero), "no impulse");
            Assert.That(activated, Is.True);
            Assert.That(kit.SpeedScale, Is.EqualTo(1.35f));
            Assert.That(kit.ThrustScale, Is.EqualTo(1.5f));
            Assert.That(kit.PaidActivations, Is.EqualTo(0), "under the base cap pays nothing");

            kit.Step(in fast, 0f, Dt, out _, 32f);
            Assert.That(kit.PaidActivations, Is.EqualTo(1), "flying past the base cap pays");

            while (kit.Active) kit.Step(in slow, 0f, Dt, out _, 32f);
            Assert.That(kit.SpeedScale, Is.EqualTo(1f));
            Assert.That(kit.ThrustScale, Is.EqualTo(1f));
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
        public void Vanguard_CarriesTheGuardDash()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var guard = catalog.Chassis.Single(c => c.name == "Vanguard").KeystonePrefab as GuardDash;
            Assert.That(guard, Is.Not.Null, "Vanguard's kit is the Guard Dash");
            Assert.That(guard.Window, Is.EqualTo(0.4f));
            Assert.That(guard.GetComponent<KeystoneFlash>(), Is.Not.Null, "the kit carries its tell");
        }

        [TestCase("Vanguard", 9000f, 5f)]
        [TestCase("Bulwark", 13500f, 7f)]
        [TestCase("Crimson", 7000f, 4f)]
        [TestCase("Nightshade", 8500f, 4.5f)]
        [TestCase("Junker_1", 13000f, 7f)]
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
    }
}
