using System.Collections.Generic;
using NUnit.Framework;
using Ships.Command;
using Ships.Weapons;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Weapons")]
    public class WeaponHardpointEditModeTests
    {
        private readonly List<GameObject> spawned = new();

        private Transform NewTransform(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go.transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test]
        public void ResolveMount_ReturnsAuthoredMount_ForMatchingSlot()
        {
            var fallback = NewTransform("root");
            var primaryMount = NewTransform("Hardpoint_Primary");
            var secondaryMount = NewTransform("Hardpoint_Secondary");
            var hardpoints = new[]
            {
                new WeaponsController.Hardpoint { slot = WeaponSlot.Primary, mount = primaryMount },
                new WeaponsController.Hardpoint { slot = WeaponSlot.Secondary, mount = secondaryMount },
            };

            Assert.AreSame(primaryMount, WeaponsController.ResolveMount(hardpoints, WeaponSlot.Primary, fallback));
            Assert.AreSame(secondaryMount, WeaponsController.ResolveMount(hardpoints, WeaponSlot.Secondary, fallback));
        }

        [Test]
        public void ResolveMount_FallsBackToRoot_WhenSlotUnmapped()
        {
            var fallback = NewTransform("root");
            var hardpoints = new[]
            {
                new WeaponsController.Hardpoint { slot = WeaponSlot.Primary, mount = NewTransform("Hardpoint_Primary") },
            };

            // Secondary has no entry → falls back to the ship origin.
            Assert.AreSame(fallback, WeaponsController.ResolveMount(hardpoints, WeaponSlot.Secondary, fallback));
        }

        [Test]
        public void ResolveMount_FallsBackToRoot_WhenArrayNullOrEntryHasNoMount()
        {
            var fallback = NewTransform("root");

            Assert.AreSame(fallback, WeaponsController.ResolveMount(null, WeaponSlot.Primary, fallback));

            var missingMount = new[]
            {
                new WeaponsController.Hardpoint { slot = WeaponSlot.Primary, mount = null },
            };
            Assert.AreSame(fallback, WeaponsController.ResolveMount(missingMount, WeaponSlot.Primary, fallback));
        }
    }
}
