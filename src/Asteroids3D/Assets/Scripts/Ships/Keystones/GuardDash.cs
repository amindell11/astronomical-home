using Balance;
using Damage;
using UnityEngine;

namespace Ships.Keystones
{
    public sealed class GuardDash : Dash
    {
        [Tooltip("Seconds after activation during which every hit on the ship is dropped.")]
        [Stat, SerializeField] private float window = 0.4f;

        public override string DisplayName => "Guard Dash";
        public float Window => window;
        public bool Guarding => SinceActivation < window;

        public override bool Blocks(in DamageInfo hit) => Guarding;
    }
}
