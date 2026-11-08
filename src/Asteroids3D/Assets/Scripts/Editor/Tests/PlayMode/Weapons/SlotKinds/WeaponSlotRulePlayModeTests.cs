#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Combat.Weapons;
using NUnit.Framework;
using Ships.Weapons;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Weapons.SlotKinds
{
    [Category("Weapons")]
    public class WeaponSlotRulePlayModeTests : PlayModeWorldFixture
    {
        private const string WeaponsPath = "Assets/Prefabs/Weapons/";

        private readonly List<GameObject> spawned = new();

        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        [TestCase("Missiles", null, "weapon slot", "Missiles")]
        [TestCase("Grenades", "Missiles", "weapon slot", "Grenades")]
        [TestCase("Lasers", "Rippers", "ordnance slot", "Rippers")]
        [TestCase(null, "Railgun", "ordnance slot", "Railgun")]
        public void Awake_RefusesTheWrongKind_BeforeEitherSlotMounts(string primary, string secondary, string slot,
            string refused)
        {
            var controller = NewRig(Prefab(primary), Prefab(secondary));

            LogAssert.Expect(LogType.Exception, new Regex($"{slot} .*'{refused}'"));
            controller.gameObject.SetActive(true);

            Assert.That(controller.Primary, Is.Null, "the weapon slot stays empty");
            Assert.That(controller.Secondary, Is.Null, "the ordnance slot stays empty");
            Assert.That(controller.GetComponentsInChildren<WeaponComponent>(true), Is.Empty, "nothing mounted");
        }

        [TestCase("Missiles", "Grenades", "weapon slot", "Missiles")]
        [TestCase("Rippers", "Railgun", "ordnance slot", "Railgun")]
        public void Reequip_RefusesTheWrongKind_BeforeEitherSlotChanges(string primary, string secondary, string slot,
            string refused)
        {
            var lasers = Prefab("Lasers");
            var missiles = Prefab("Missiles");
            var controller = NewRig(lasers, missiles);
            controller.gameObject.SetActive(true);
            var weapon = controller.Primary;
            var ordnance = controller.Secondary;

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                controller.Reequip(Prefab(primary), Prefab(secondary)));

            StringAssert.Contains(slot, thrown.Message);
            StringAssert.Contains($"'{refused}'", thrown.Message);
            Assert.That(controller.Primary, Is.SameAs(weapon), "the weapon slot keeps its mount");
            Assert.That(controller.Secondary, Is.SameAs(ordnance), "the ordnance slot keeps its mount");
            Assert.That(controller.PrimaryMountPrefab, Is.SameAs(lasers));
            Assert.That(controller.SecondaryMountPrefab, Is.SameAs(missiles));
        }

        private WeaponsController NewRig(WeaponComponent primary, WeaponComponent secondary)
        {
            var go = new GameObject("Weapon rig");
            spawned.Add(go);
            go.SetActive(false);
            go.transform.rotation = GamePlane.Rotation;

            var controller = go.AddComponent<WeaponsController>();
            controller.centreSocket = Socket(go.transform, "Centre");
            controller.leftSocket = Socket(go.transform, "Left");
            controller.rightSocket = Socket(go.transform, "Right");
            controller.primaryMount = primary;
            controller.secondaryMount = secondary;
            return controller;
        }

        private static Transform Socket(Transform rig, string name)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(rig, false);
            return socket;
        }

        private static WeaponComponent Prefab(string name)
        {
            if (name == null) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<WeaponComponent>(WeaponsPath + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, name);
            return prefab;
        }
    }
}
#endif
