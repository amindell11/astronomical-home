using UnityEngine;

namespace Ships
{
    /// <summary>
    /// Movement/handling stats for a ship. One of the composable modules a <see cref="FrameSettings"/>
    /// references as its default engine. Owns every movement-related field (disjoint ownership: the
    /// resolver copies these verbatim into <see cref="ResolvedShipStats"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "EngineModule", menuName = "Ship/Modules/Engine")]
    public class EngineModule : TunableModule
    {
        [Header("Movement")]
        public float maxSpeed = 25f;
        public float maxYawRate = 180f;
        public float forwardForce = 7000f;
        public float reverseForce = 3500f;
        public float yawTorque = 7000;
        public float angularDrag = 1.7f;
        public float bankTorque = 5000f;
        public float bankDamping = 200f;
        public float minStrafeForce = 4000f;
        public float maxStrafeForce = 5000f;
        public float linearDrag = .5f;

        [Header("Boost")]
        public float boostImpulse = 14000f;
        public float boostCooldown = 3f;
    }
}
