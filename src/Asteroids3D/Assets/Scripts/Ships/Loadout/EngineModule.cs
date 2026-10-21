using Balance;
using UnityEngine;

namespace Ships.Loadout
{
    /// <summary>
    /// Movement/handling stats for a ship. One of the composable modules a <see cref="Ship"/> carries.
    /// Owns every movement-related field (disjoint ownership: the resolver copies these verbatim into
    /// <see cref="ResolvedShipStats"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "EngineModule", menuName = "Ship/Modules/Engine")]
    public class EngineModule : TunableModule
    {
        [Header("Movement")]
        [Stat] public float maxSpeed = 25f;
        [Stat] public float maxYawRate = 180f;
        [Stat] public float forwardForce = 7000f;
        [Stat] public float reverseForce = 3500f;
        [Stat] public float yawTorque = 7000;
        [Stat] public float angularDrag = 1.7f;
        [Stat] public float bankTorque = 5000f;
        [Stat] public float bankDamping = 200f;
        [Stat] public float minStrafeForce = 4000f;
        [Stat] public float maxStrafeForce = 5000f;
        [Stat] public float linearDrag = .5f;

        [Header("Boost")]
        [Stat] public float boostImpulse = 14000f;
        [Stat] public float boostCooldown = 3f;
    }
}
