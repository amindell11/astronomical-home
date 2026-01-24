using UnityEngine;

namespace Ships.Movement
{
    public class Booster
    {        
        private float nextBoostTime;

        public bool BoostAvailable => Time.time > nextBoostTime;
        
        internal int ProcessBoost(float input, float cooldown)
        {
            if (!BoostAvailable || input == 0) return 0;
            nextBoostTime = Time.time + cooldown;
            return 1;
        }

    }
}