using UnityEngine;
using UnityEngine.Serialization;

namespace ShipMain
{
    [CreateAssetMenu(fileName = "ShipSettings", menuName = "Ship/ShipSettings")]
    public class Settings : ScriptableObject
    {
        [Header("Movement")] 
        public float mass = 215;
        public float maxSpeed = 15f;
        public float maxRotationSpeed = 180f;
        [FormerlySerializedAs("forwardAcceleration")] public float forwardAccel = 1200f;
        [FormerlySerializedAs("reverseAcceleration")] public float reverseAccel = 600f;
        [FormerlySerializedAs("rotationThrustForce")] public float rotationThrust = 480f;
        public float rotationDrag        = 0.95f;
        [FormerlySerializedAs("yawDeadzoneAngle")] public float yawDeadZone    = 4f;
        public float maxBankAngle        = 45f;
        public float bankingSpeed        = 5f;
        public float minStrafeForce      = 750f;
        public float maxStrafeForce      = 800f;
        public float linearDrag = .2f;

        [Header("Boost")]
        [Tooltip("Impulse applied when the ship boosts (units of force).")]
        public float boostImpulse = 4000f;
        [Tooltip("Cooldown time between boost activations (seconds)")]
        public float boostCooldown = 3f;

        [Header("Damage & Health")]
        public float maxHealth     = 100f;
        public float maxShield     = 50f;
        public int   startingLives = 1;
        public float shieldRegenDelay = 3f;
        public float shieldRegenRate  = 10f;
    }
}