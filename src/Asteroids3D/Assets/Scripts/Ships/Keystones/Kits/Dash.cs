using Balance;
using Movement;
using UnityEngine;

namespace Ships.Keystones.Kits
{
    public class Dash : Keystone
    {
        [Tooltip("Forward impulse applied on activation (N·s).")]
        [Stat, SerializeField] private float impulse = 9000f;

        public override string DisplayName => "Dash";
        public float Impulse => impulse;

        protected override KeystoneEffect Activate(in Kinematics kinematics, Vector2? target) => kinematics.Forward * impulse;
    }
}
