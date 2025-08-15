using UnityEngine;

namespace ShipMain.Movement
{
    internal class Booster
    {        
        private float nextBoostTime = 0f;
        
        public bool BoostAvailable => Time.time > nextBoostTime;


        internal Vector2 Boost(Kinematics kin, float boost, float boostStrength, float cooldown)
        {
            if (!BoostAvailable || boost == 0) return Vector2.zero;
            nextBoostTime = Time.time + cooldown;
            return Calculator.Boost(kin,  boost, boostStrength);
        }
    }
}