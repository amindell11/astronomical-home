using System;
using System.Globalization;
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
        public bool shieldNumbers;
        public float shieldCapacity;
        public float shieldDelay;
        public float shieldRate;

        public string Label
        {
            get
            {
                var body = $"{chassis.name}-{pilot.name}-{weapon.name}";
                if (shieldNumbers)
                    return body + "-S" + Num(shieldCapacity) + "_" + Num(shieldDelay) + "_" + Num(shieldRate);
                return shield ? $"{body}-{shield.name}" : body;
            }
        }

        private static string Num(float value) => value.ToString("G9", CultureInfo.InvariantCulture);
    }
}
