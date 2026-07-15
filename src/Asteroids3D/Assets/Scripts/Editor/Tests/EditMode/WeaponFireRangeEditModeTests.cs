using System;
using System.Collections.Generic;
using Combat.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Weapons")]
    public class WeaponFireRangeEditModeTests
    {
        private readonly List<GameObject> spawned = new();

        private WeaponComponent NewWeapon(Type weaponType)
        {
            var go = new GameObject(weaponType.Name);
            go.SetActive(false);
            spawned.Add(go);
            return (WeaponComponent)go.AddComponent(weaponType);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
                if (go) UnityEngine.Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [TestCase(typeof(Lasers))]
        [TestCase(typeof(ChargeLasers))]
        [TestCase(typeof(Railguns))]
        [TestCase(typeof(Rippers))]
        public void FireRange_MirrorsSerializedFireDistance(Type weaponType)
        {
            var weapon = NewWeapon(weaponType);
            var fireDistance = new SerializedObject(weapon).FindProperty("fireDistance");

            Assert.IsNotNull(fireDistance, $"{weaponType.Name} must carry a serialized fireDistance field");
            Assert.Greater(fireDistance.floatValue, 0f);
            Assert.AreEqual(fireDistance.floatValue, weapon.FireRange);
        }

        [Category("Smoke")]
        [TestCase(typeof(Missiles))]
        [TestCase(typeof(Grenades))]
        public void FireRange_DefaultsToZero_ForWeaponsWithoutDistanceGate(Type weaponType)
        {
            Assert.AreEqual(0f, NewWeapon(weaponType).FireRange);
        }
    }
}
