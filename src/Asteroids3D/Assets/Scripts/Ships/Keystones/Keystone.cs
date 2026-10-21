using Balance;
using Movement;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// The locked ability in a ship's keystone slot, one kit per ship. The ship instantiates its kit
    /// prefab and hands the instance to <see cref="Ships.Movement.MovementController"/>, which steps it
    /// every fixed step with the activation bit. The cooldown runs on the kit's own fixed-step clock,
    /// never <c>Time.time</c>, so pacing replays identically across respawns and arenas. A kit's
    /// effects beyond the activation impulse are state on the kit that other systems observe.
    /// Design: https://github.com/amindell11/astronomical-home/issues/1028
    /// </summary>
    public abstract class Keystone : MonoBehaviour
    {
        [Tooltip("Seconds from one activation until the next is ready.")]
        [Stat, SerializeField] private float cooldown = 5f;

        private float clock;
        private float readyAt;

        public abstract string DisplayName { get; }
        public float Cooldown => cooldown;
        public bool Ready => clock >= readyAt;
        public float CooldownRemaining => Mathf.Max(0f, readyAt - clock);
        public float CooldownPct => cooldown > 0f ? CooldownRemaining / cooldown : 0f;

        public Vector2 Step(in Kinematics kinematics, float input, float dt, out bool activated)
        {
            clock += dt;
            activated = Ready && input != 0f;
            if (!activated) return Vector2.zero;
            readyAt = clock + cooldown;
            return Activate(in kinematics);
        }

        public void ResetKeystone()
        {
            clock = 0f;
            readyAt = 0f;
        }

        protected abstract Vector2 Activate(in Kinematics kinematics);
    }
}
