#if UNITY_EDITOR
using System.Collections;
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

namespace Tests.PlayMode.Weapons
{
    /// <summary>
    /// Where shots leave the hull: a single-fire weapon fires every shot from the centre socket, a
    /// dual-fire weapon alternates left and right with one shot and one fire event each, and a
    /// controller without all three sockets refuses to mount anything.
    /// </summary>
    [Category("Weapons")]
    public class WeaponFireOriginPlayModeTests : PlayModeWorldFixture
    {
        private const string WeaponsPath = "Assets/Prefabs/Weapons/";
        private const int ShotTimeoutSteps = 100;

        private static readonly Vector3 Ahead = GamePlane.PlanePointToWorld(new Vector2(0f, 20f));

        private readonly List<GameObject> spawned = new();

        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        [TestCase("Lasers", false)]
        [TestCase("TurboLasers", true)]
        [TestCase("Rippers", true)]
        [TestCase("Railgun", false)]
        [TestCase("Missiles", false)]
        [TestCase("Grenades", false)]
        public void OnlyTurboLasersAndRippersAreDualFire(string weapon, bool dualFire)
        {
            Assert.That(Prefab(weapon).DualFire, Is.EqualTo(dualFire));
        }

        [UnityTest]
        public IEnumerator Lasers_FireEveryShotFromTheCentre() =>
            ShotsLeaveFrom("Lasers", "Centre", "Centre", "Centre");

        [UnityTest]
        public IEnumerator TurboLasers_AlternateLeftAndRight() =>
            ShotsLeaveFrom("TurboLasers", "Left", "Right", "Left", "Right");

        [UnityTest]
        public IEnumerator Rippers_AlternateLeftAndRight() =>
            ShotsLeaveFrom("Rippers", "Left", "Right", "Left", "Right");

        [TestCase("Centre")]
        [TestCase("Left")]
        [TestCase("Right")]
        public void MissingSocket_ThrowsAtSetup_AndNamesIt(string side)
        {
            var controller = NewRig(Prefab("Lasers"));
            switch (side)
            {
                case "Centre": controller.centreSocket = null; break;
                case "Left": controller.leftSocket = null; break;
                case "Right": controller.rightSocket = null; break;
            }

            LogAssert.Expect(LogType.Exception, new Regex($"{side} socket"));
            controller.gameObject.SetActive(true);

            Assert.That(controller.Primary, Is.Null, "Nothing mounts without all three sockets.");
        }

        // The fire event sees the muzzle that fired, as the fire sound does.
        private IEnumerator ShotsLeaveFrom(string weaponName, params string[] sides)
        {
            var controller = NewRig(Prefab(weaponName));
            controller.gameObject.SetActive(true);
            var weapon = controller.Primary;
            var fireEvents = new List<Vector3>();
            weapon.OnFire += () => fireEvents.Add(weapon.firePoint.position);

            for (var shot = 0; shot < sides.Length; shot++)
            {
                for (var step = 0; !weapon.CanFire() && step < ShotTimeoutSteps; step++)
                    yield return new WaitForFixedUpdate();
                var socket = controller.transform.Find(sides[shot]).position;
                var projectile = weapon.Fire(Ahead, Projectiles);

                Assert.That(projectile, Is.Not.Null, $"{weaponName} refused shot {shot}.");
                Assert.That(fireEvents.Count, Is.EqualTo(shot + 1), "one fire event per shot");
                Assert.That(Vector3.Distance(projectile.transform.position, socket), Is.LessThan(1e-4f),
                    $"{weaponName} shot {shot} leaves from {sides[shot]}.");
                Assert.That(Vector3.Distance(fireEvents[shot], socket), Is.LessThan(1e-4f),
                    $"{weaponName} shot {shot} sounds at {sides[shot]}.");
            }
        }

        private WeaponsController NewRig(WeaponComponent primary)
        {
            var go = new GameObject("Weapon rig");
            spawned.Add(go);
            go.SetActive(false);
            go.transform.rotation = GamePlane.Rotation;

            var controller = go.AddComponent<WeaponsController>();
            controller.centreSocket = Socket(go.transform, "Centre", new Vector3(0f, 1f, 0f));
            controller.leftSocket = Socket(go.transform, "Left", new Vector3(-0.5f, 0f, 0f));
            controller.rightSocket = Socket(go.transform, "Right", new Vector3(0.5f, 0f, 0f));
            controller.primaryMount = primary;
            return controller;
        }

        private static Transform Socket(Transform rig, string name, Vector3 localPosition)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(rig, false);
            socket.localPosition = localPosition;
            return socket;
        }

        private static WeaponComponent Prefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<WeaponComponent>(WeaponsPath + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, name);
            return prefab;
        }
    }
}
#endif
