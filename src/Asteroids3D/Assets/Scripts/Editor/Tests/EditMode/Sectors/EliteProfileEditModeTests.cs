using System;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Substrate.Sectors.Elements.Elites;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Sectors
{
    [TestFixture]
    [Category("Sectors")]
    public class EliteProfileEditModeTests
    {
        private EliteProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<EliteProfile>();
            profile.name = "TestElite";
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        private static WeaponComponent Primary()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Vanguard.prefab");
            Assert.IsTrue(ship && ship.Weapons && ship.Weapons.PrimaryMountPrefab, "test premise: Vanguard carries a primary");
            return ship.Weapons.PrimaryMountPrefab;
        }

        [Test]
        public void Validate_Throws_WhenStepsAreNotInDescendingHullOrder()
        {
            profile.Configure("Test elite", ShipScale.One, Primary(), escalation: new[]
            {
                new EscalationStep { hull = 1f / 3f, escorts = 1 },
                new EscalationStep { hull = 2f / 3f, escorts = 2 },
            });

            var thrown = Assert.Throws<InvalidOperationException>(() => profile.Validate());
            StringAssert.Contains("TestElite", thrown.Message);
        }

        [Test]
        public void Validate_Throws_WithoutAPrimaryWeapon()
        {
            profile.Configure("Test elite", ShipScale.One, null);

            var thrown = Assert.Throws<InvalidOperationException>(() => profile.Validate());
            StringAssert.Contains("TestElite", thrown.Message);
        }
    }
}
