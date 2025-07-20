using UnityEngine;

[CreateAssetMenu(fileName = "ShipSettings", menuName = "Ship/ShipSettings")]
public class ShipSettings : ScriptableObject
{
    [Header("Movement")]
    public float maxSpeed = 15f;
    public float maxRotationSpeed = 180f;
    public float forwardAcceleration = 1200f;
    public float reverseAcceleration = 600f;
    public float rotationThrustForce = 480f;
    public float rotationDrag        = 0.95f;
    public float yawDeadzoneAngle    = 4f;
    public float maxBankAngle        = 45f;
    public float bankingSpeed        = 5f;
    public float minStrafeForce      = 750f;
    public float maxStrafeForce      = 800f;

    [Header("Boost")]
    [Tooltip("Impulse applied when the ship boosts (units of force).")]
    public float boostImpulse = 5000f;
    [Tooltip("Cooldown time between boost activations (seconds). Not yet enforced by code but available for future use.")]
    public float boostCooldown = 2f;

    [Header("Damage & Health")]
    public float maxHealth     = 100f;
    public float maxShield     = 50f;
    public int   startingLives = 3;
    public float shieldRegenDelay = 3f;
    public float shieldRegenRate  = 10f;
}