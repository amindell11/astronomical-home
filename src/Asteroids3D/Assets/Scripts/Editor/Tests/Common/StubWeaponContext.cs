using System.Collections.Generic;
using Combat.Weapons;
using Ships.Command;

namespace Tests.Common
{
    public sealed class StubWeaponContext : IWeaponContext
    {
        private static readonly WeaponSlot[] slots = { WeaponSlot.Primary };
        public IReadOnlyList<WeaponSlot> Slots => slots;
        public bool IsReady(WeaponSlot slot) => true;
        public float ProjectileSpeed(WeaponSlot slot) => 40f;
        public Gunsight Sight(WeaponSlot slot) => null;
    }
}
