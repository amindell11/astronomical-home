using System;
using AI;
using Combat.Weapons;
using Ships;

namespace RL.Hosts
{
    [Serializable]
    public struct MatrixSide
    {
        public Ship chassis;
        public AICommander pilot;
        public WeaponComponent weapon;

        public string Label => $"{chassis.name}-{pilot.name}-{weapon.name}";
    }
}
