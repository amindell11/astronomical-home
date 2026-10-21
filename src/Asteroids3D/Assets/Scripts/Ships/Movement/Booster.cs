using UnityEngine;

namespace Ships.Movement
{
    public class Booster
    {
        // Internal dt-driven clock (mirrors Heat): resets on respawn, so boost pacing replays identically regardless of absolute session time.
        private float clock;
        private float nextBoostTime;

        public bool BoostAvailable => clock >= nextBoostTime;
        public float CooldownRemaining => Mathf.Max(0f, nextBoostTime - clock);

        internal void Tick(float dt) => clock += dt;

        internal int ProcessBoost(float input, float cooldown)
        {
            if (!BoostAvailable || input == 0) return 0;
            nextBoostTime = clock + cooldown;
            return 1;
        }

        internal void Reset()
        {
            clock = 0f;
            nextBoostTime = 0f;
        }
    }
}
