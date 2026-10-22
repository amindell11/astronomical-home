using Balance;
using Movement;
using UnityEngine;

namespace Ships.Keystones
{
    public class Dash : Keystone
    {
        [Tooltip("Forward impulse applied on activation (N·s).")]
        [Stat, SerializeField] private float impulse = 9000f;

        public override string DisplayName => "Dash";
        public float Impulse => impulse;

        protected override Vector2 Activate(in Kinematics kinematics) => kinematics.Forward * impulse;
    }
}
