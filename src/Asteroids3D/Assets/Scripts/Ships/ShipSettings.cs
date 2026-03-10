using System;
using Movement;
using UnityEngine;
using UnityEngine.Events;

namespace Ships
{
    [CreateAssetMenu(fileName = "ShipSettings", menuName = "Ship/ShipSettings")]
    public class ShipSettings : ScriptableObject
    {
        [Header("Movement")] 
        public float mass = 1000;
        public float maxSpeed = 25f;
        public float maxYawRate = 180f;
        public float forwardForce = 7000f;
        public float reverseForce = 3500f;
        public float yawTorque = 7000;
        public float angularDrag = 1.7f;
        public float maxBankAngle = 45f; //visual only
        public float bankingSpeed = 5f; //visual only
        public float minStrafeForce = 4000f;
        public float maxStrafeForce = 5000f;
        public float linearDrag = .5f;

        [Header("Boost")]
        public float boostImpulse = 14000f;
        public float boostCooldown = 3f;

        [Header("Damage & Health")]
        public int startingLives = 1;
        public float maxHealth = 100f;
        public float maxShield = 50f;
        public float shieldRegenDelay = 4f;
        public float shieldRegenRate = 10f;

        [NonSerialized]
        public readonly UnityEvent onSettingsChanged = new UnityEvent();

        private void OnValidate()
        {
            onSettingsChanged?.Invoke();
        }

        public Dynamics Dynamics => BuildDynamics(0f);

        public Dynamics BuildDynamics(float yawInertia) => new Dynamics
        (
            mass : mass,
            forwardAcc : forwardForce,
            reverseAcc : reverseForce,
            maxStrafeAcc : maxStrafeForce,
            minStrafeAcc : minStrafeForce,
            maxSpeed : maxSpeed,
            maxYawRate : maxYawRate * Mathf.Deg2Rad,
            yawTorque : yawTorque * Mathf.Deg2Rad,
            angularDrag : angularDrag,
            linearDrag: linearDrag,
            yawInertia: yawInertia > 0f ? yawInertia : mass
        );

    }
}