#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using NUnit.Framework;
using Ships.Command;
using Ships.Weapons;
using Substrate;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons.FireOrigins
{
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

        [TestCase("Lasers", FirePattern.Single)]
        [TestCase("TurboLasers", FirePattern.Alternating)]
        [TestCase("Rippers", FirePattern.Volley)]
        [TestCase("Railgun", FirePattern.Single)]
        [TestCase("Missiles", FirePattern.Single)]
        [TestCase("Grenades", FirePattern.Single)]
        public void OnlyTurboLasersAlternate_AndOnlyRippersFireVolleys(string weapon, FirePattern pattern)
        {
            Assert.That(Prefab(weapon).FirePattern, Is.EqualTo(pattern));
        }

        [UnityTest]
        public IEnumerator Lasers_FireEveryShotFromTheCentre() =>
            ShotsLeaveFrom("Lasers", "Centre", "Centre", "Centre");

        [UnityTest]
        public IEnumerator TurboLasers_AlternateLeftAndRight() =>
            ShotsLeaveFrom("TurboLasers", "Left", "Right", "Left", "Right");

        [UnityTest]
        public IEnumerator Rippers_FireBothSidesEveryShot_OnOneRoundAndOneFireEvent()
        {
            var controller = NewRig(Prefab("Rippers"));
            controller.gameObject.SetActive(true);
            controller.Initialize(() => default);
            var rippers = (Rippers)controller.Primary;
            var fireEvents = new List<Vector3>();
            rippers.OnFire += () => fireEvents.Add(rippers.firePoint.position);
            var centre = SocketPosition(controller, "Centre");

            Assert.That(Vector3.Distance(controller.Context.Sight(WeaponSlot.Primary).FirePoint, centre), Is.LessThan(1e-4f),
                "the gunsight sights a volley from the centre");

            for (var shot = 0; shot < 2; shot++)
            {
                yield return UntilFirable(rippers);
                var rounds = rippers.Rounds.AmmoCount;
                var before = Live();
                Assert.That(rippers.Fire(Ahead, Projectiles), Is.Not.Null, $"Rippers refused shot {shot}.");
                var slugs = Live().Except(before).Cast<ProjectileBase>().ToList();

                Assert.That(slugs.Select(slug => SideAt(controller, slug.transform.position)),
                    Is.EquivalentTo(new[] { "Left", "Right" }), $"shot {shot} leaves from both sides");
                foreach (var slug in slugs)
                    Assert.That(slug.Damage * slug.DamageScale, Is.EqualTo(rippers.Damage), "each slug does full damage");
                Assert.That(rippers.Rounds.AmmoCount, Is.EqualTo(rounds - 1), "a volley spends one round");
                Assert.That(fireEvents.Count, Is.EqualTo(shot + 1), "one fire event per volley");
                Assert.That(Vector3.Distance(fireEvents[shot], centre), Is.LessThan(1e-4f), "a volley sounds at the centre");
            }
        }

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
                yield return UntilFirable(weapon);
                var socket = SocketPosition(controller, sides[shot]);
                var before = Live();
                var projectile = weapon.Fire(Ahead, Projectiles);

                Assert.That(projectile, Is.Not.Null, $"{weaponName} refused shot {shot}.");
                Assert.That(Live().Except(before).Count(), Is.EqualTo(1), "one projectile per shot");
                Assert.That(fireEvents.Count, Is.EqualTo(shot + 1), "one fire event per shot");
                Assert.That(Vector3.Distance(projectile.transform.position, socket), Is.LessThan(1e-4f),
                    $"{weaponName} shot {shot} leaves from {sides[shot]}.");
                Assert.That(Vector3.Distance(fireEvents[shot], socket), Is.LessThan(1e-4f),
                    $"{weaponName} shot {shot} sounds at {sides[shot]}.");
            }
        }

        private static IEnumerator UntilFirable(WeaponComponent weapon)
        {
            for (var step = 0; !weapon.CanFire() && step < ShotTimeoutSteps; step++)
                yield return new WaitForFixedUpdate();
        }

        private List<MonoBehaviour> Live()
        {
            var live = new List<MonoBehaviour>();
            Projectiles.ForEachLive(live.Add);
            return live;
        }

        private static Vector3 SocketPosition(WeaponsController rig, string side) => rig.transform.Find(side).position;

        private static string SideAt(WeaponsController rig, Vector3 position) =>
            new[] { "Centre", "Left", "Right" }.FirstOrDefault(side =>
                Vector3.Distance(SocketPosition(rig, side), position) < 1e-4f);

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
