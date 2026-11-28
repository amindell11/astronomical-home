#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Mods;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Weapons
{
    /// <summary>With Wire guided held, a missile flies at the trigger's aim point as it moves each step, with no lock.</summary>
    [Category("Weapons")]
    public class MissileWireGuidedPlayModeTests : PlayModeWorldFixture
    {
        private const string MissilesPath = "Assets/Prefabs/Weapons/Missiles.prefab";
        private const string WireGuidedPath = "Assets/Settings/Weapons/Mods/Behaviours/WireGuided.asset";

        private readonly List<GameObject> spawned = new();
        private Ship ship;
        private IWeapons weapons;
        private Missiles launcher;

        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        private void Arm(bool wireGuided)
        {
            ship = Object.Instantiate(TestAssets.LoadCrimsonPrefab(), Vector3.zero, GamePlane.Rotation);
            spawned.Add(ship.gameObject);
            ship.Initialize(0, 0, Projectiles);
            ship.Reequip(null, TestAssets.Load<Missiles>(MissilesPath));
            launcher = (Missiles)ship.Weapons.Secondary;
            if (wireGuided)
                launcher.Behaviours.Set(new[] { TestAssets.Load<WeaponBehaviourMod>(WireGuidedPath) });
            weapons = ship.Weapons.Arm(Projectiles);
        }

        private Vector3 Side => Vector3.Cross(GamePlane.Normal, launcher.FireDirection).normalized;

        private Missile Launch(Vector3 aim)
        {
            Missile fired = null;
            weapons.Fire(WeaponSlot.Secondary, new WeaponCommand { held = true, pressed = true, targetPoint = aim });
            Projectiles.ForEachLive(live => fired = live as Missile);
            Assert.IsNotNull(fired, "the press launches a missile");
            return fired;
        }

        private IEnumerator Steer(Vector3 aim, int steps)
        {
            for (var step = 0; step < steps; step++)
            {
                weapons.Fire(WeaponSlot.Secondary, new WeaponCommand { targetPoint = aim });
                yield return new WaitForFixedUpdate();
            }
        }

        private static float Along(Missile missile, Vector3 axis) =>
            Vector3.Dot(missile.GetComponent<Rigidbody>().linearVelocity.normalized, axis);

        [UnityTest]
        public IEnumerator WithoutIt_AnUnlockedMissileFliesStraight()
        {
            Arm(wireGuided: false);
            var aim = launcher.FireOrigin + Side * 15f;
            var missile = Launch(aim);

            yield return Steer(aim, 30);

            Assert.AreEqual(0f, Along(missile, Side), 0.05f, "no lock, no steering");
        }

        [UnityTest]
        public IEnumerator WireGuided_FollowsTheAimPointAsItMoves()
        {
            Arm(wireGuided: true);
            var right = launcher.FireOrigin + launcher.FireDirection * 10f + Side * 15f;
            var left = launcher.FireOrigin + launcher.FireDirection * 10f - Side * 15f;
            var missile = Launch(right);

            yield return Steer(right, 30);
            var towardRight = Along(missile, Side);
            Assert.Greater(towardRight, 0.3f, "it turns toward the aim point");

            yield return Steer(left, 40);
            Assert.Less(Along(missile, Side), towardRight - 0.3f, "it turns after the aim point when it moves");
            Assert.IsFalse(missile.IsTracking, "it steers by the wire, not a lock");
            Assert.IsTrue(missile.IsWired && launcher.WireLive, "the launcher reports its wire live");

            missile.ReturnToPoolImmediate();
            Assert.IsFalse(launcher.WireLive, "the wire dies with the missile");

            // Stands in for another launcher taking the same pooled missile and wiring it.
            missile.gameObject.SetActive(true);
            missile.Wire(() => Vector3.zero);
            Assert.IsFalse(launcher.WireLive, "a reused missile another launcher wires is not this launcher's");
            missile.gameObject.SetActive(false);
        }
    }
}
#endif
