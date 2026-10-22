using System.Collections.Generic;
using AI;
using Combat.Weapons;
using Movement;
using Ships.Command;
using UnityEngine;

namespace Tests.Common
{
    public sealed class StubWeaponContext : IWeaponContext
    {
        private static readonly WeaponSlot[] slots = { WeaponSlot.Primary };
        public IReadOnlyList<WeaponSlot> Slots => slots;
        public bool IsReady(WeaponSlot slot) => true;
        public float ProjectileSpeed(WeaponSlot slot) => 40f;
        public Vector2 AimPoint(WeaponSlot slot, in Kinematics shooter, Vector2 targetPos, Vector2 targetVel) =>
            Gunner.AimPoint(in shooter, targetPos, targetVel, ProjectileSpeed(slot));
        public float? HoldRange(WeaponSlot slot) => null;
        public Gunsight Sight(WeaponSlot slot) => null;
    }
}
