using System;
using AI;
using Combat.Weapons;
using Ships;
using Ships.Loadout;

namespace RL.Hosts
{
    [Serializable]
    public struct MatrixSide
    {
        public Ship chassis;
        public AICommander pilot;
        public WeaponComponent weapon;
        public ShieldModule shield;

        public string Label => shield
            ? $"{chassis.name}-{pilot.name}-{weapon.name}-{shield.name}"
            : $"{chassis.name}-{pilot.name}-{weapon.name}";
    }
}
