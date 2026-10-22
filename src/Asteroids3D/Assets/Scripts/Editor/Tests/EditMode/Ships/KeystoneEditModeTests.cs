using System;
using System.Linq;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Keystones;
using Ships.Loadout;
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

        private Dash NewDash()
        {
            host = new GameObject("Dash");
            var dash = host.AddComponent<Dash>();
            var so = new SerializedObject(dash);
            so.FindProperty("impulse").floatValue = Impulse;
            so.FindProperty("cooldown").floatValue = Cooldown;
            so.ApplyModifiedPropertiesWithoutUndo();
            return dash;
        }

        private static Kinematics Facing(float yawDeg) => new(Vector2.zero, Vector2.zero, yawDeg, 0f, 0f);

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

        [TestCase("Vanguard", 9000f, 5f)]
        [TestCase("Bulwark", 13500f, 7f)]
        [TestCase("Valis", 8000f, 4f)]
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
