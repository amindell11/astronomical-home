using Combat.Weapons;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Weapons
{
    [TestFixture]
    [Category("Weapons")]
    public class WeaponHoldRangeEditModeTests
    {
        [TestCase("Assets/Prefabs/Weapons/Railgun.prefab", 30f)]
        [TestCase("Assets/Prefabs/Weapons/Missiles.prefab", 13f)]
        [TestCase("Assets/Prefabs/Weapons/Grenades.prefab", 18f)]
        public void Prefab_DeclaresHoldRange(string path, float expected)
        {
            var weapon = Load(path);
            Assert.AreEqual((float?)expected, weapon.HoldRange, path);
        }

        [TestCase("Assets/Prefabs/Weapons/Lasers.prefab")]
        [TestCase("Assets/Prefabs/Weapons/TurboLasers.prefab")]
        [TestCase("Assets/Prefabs/Weapons/Rippers.prefab")]
        [TestCase("Assets/Prefabs/Weapons/ChargeLasers.prefab")]
        public void Prefab_LeavesHoldRangeToThePilot(string path)
        {
            Assert.IsNull(Load(path).HoldRange, path);
        }

        private static WeaponComponent Load(string path)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponComponent>(path);
            Assert.IsNotNull(weapon, path);
            return weapon;
        }
    }
}
