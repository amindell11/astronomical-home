using System.Linq;
using NUnit.Framework;
using Ships.Loadout;
using UnityEditor;

namespace Tests.EditMode.Ships
{
    [TestFixture]
    [Category("Ships")]
    public sealed class ChassisProfileEditModeTests
    {
        [TestCase("Vanguard", "Vanguard_Engine", "Default_Shield", 800f, 100f)]
        [TestCase("Bulwark", "Bulwark_Engine", "Bulwark_Shield", 1200f, 160f)]
        [TestCase("Valis", "Valis_Engine", "Default_Shield", 650f, 80f)]
        [TestCase("Crimson", "Crimson_Engine", "Light_Shield", 560f, 65f)]
        [TestCase("Nightshade", "Nightshade_Engine", "Nightshade_Shield", 700f, 85f)]
        public void Chassis_ResolvesItsOwnEngineShieldMassAndHull(string chassis, string engine, string shield, float mass, float hull)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var ship = catalog.Chassis.Single(c => c.name == chassis);
            Assert.That(ship.Engine, Is.Not.Null, $"{chassis}: no engine wired");
            Assert.That(ship.Engine.name, Is.EqualTo(engine), $"{chassis}: engine");
            Assert.That(ship.Shield, Is.Not.Null, $"{chassis}: no shield wired");
            Assert.That(ship.Shield.name, Is.EqualTo(shield), $"{chassis}: shield");
            Assert.That(ship.mass, Is.EqualTo(mass), $"{chassis}: mass");
            Assert.That(ship.maxHealth, Is.EqualTo(hull), $"{chassis}: hull");
        }
    }
}
