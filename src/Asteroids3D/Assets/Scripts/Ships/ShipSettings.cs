using AI.Steering;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Ships
{
    [CreateAssetMenu(fileName = "ShipSettings", menuName = "Ship/ShipSettings")]
    public class Settings : ScriptableObject
    {
        [Header("Movement")] 
        public float mass = 215;
        public float maxSpeed = 20f;
        public float maxYawRate = 180f;
        public float forwardAccel = 1200f;
        public float reverseAccel = 600f;
        [FormerlySerializedAs("rotationThrust")] [FormerlySerializedAs("alpha")]
        public float yawTorque = 580;
        [FormerlySerializedAs("rotationDrag")]
        public float angularDrag        = 1.2f;
        public float maxBankAngle        = 45f; //visual only
        public float bankingSpeed        = 5f; //visual only
        public float minStrafeForce      = 750f;
        public float maxStrafeForce      = 800f;
        public float linearDrag = .2f;

        [Header("Boost")]
        [Tooltip("Impulse applied when the ship boosts (units of force).")]
        public float boostImpulse = 5000f;
        [Tooltip("Cooldown time between boost activations (seconds)")]
        public float boostCooldown = 3f;

        [Header("Damage & Health")]
        public float maxHealth     = 100f;
        public float maxShield     = 50f;
        public int   startingLives = 1;
        public float shieldRegenDelay = 4f;
        public float shieldRegenRate  = 10f;

        [System.NonSerialized]
        public readonly UnityEvent onSettingsChanged = new UnityEvent();

        private void OnValidate()
        {
            onSettingsChanged?.Invoke();
        }

        public Dynamics Dynamics => new Dynamics
        (
            mass : mass,
            forwardAcc : forwardAccel,
            reverseAcc : reverseAccel,
            maxStrafeAcc : maxStrafeForce,
            minStrafeAcc : minStrafeForce,
            maxSpeed : maxSpeed,
            maxYawRate : maxYawRate * Mathf.Deg2Rad,
            yawTorque : yawTorque * Mathf.Deg2Rad,  
            angularDrag : angularDrag,
            linearDrag: linearDrag
        );

    }
}