using System;
using Game.Services;
using NUnit.Framework;
using Ships.Command;
using Ships.Weapons;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Weapons")]
    public class WeaponArmingEditModeTests
    {
        [Test]
        public void WeaponsController_IsNotItselfAFiringSurface()
        {
            Assert.IsFalse(typeof(IWeapons).IsAssignableFrom(typeof(WeaponsController)),
                "Firing must only be reachable through Arm(IProjectileService) — an ambient actuator reintroduces untracked projectiles");
        }

        [Test]
        public void Arming_DemandsTheRegistry()
        {
            var go = new GameObject("Controller");
            try
            {
                var controller = go.AddComponent<WeaponsController>();
                Assert.Throws<ArgumentNullException>(() => controller.Arm(null));
                Assert.IsNotNull(controller.Arm(new ProjectileService(go.transform)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
