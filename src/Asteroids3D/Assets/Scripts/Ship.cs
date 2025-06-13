using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Ship : MonoBehaviour
{
    [Header("Thrust Settings")]
    public float thrustForce = 5.0f;
    public float maxSpeed = 10.0f;

    [Header("Rotation Settings")]
    public float rotationThrustForce = 200.0f;
    public float maxRotationSpeed = 180.0f;
    public float rotationDrag = 0.95f;
    [Tooltip("The angle in degrees inside which the ship will not try to yaw towards the mouse.")]
    public float yawDeadzoneAngle = 2.0f;

    [Header("Banking Settings")]
    public float maxBankAngle = 45f;
    public float bankingSpeed = 5f;
    public float maxStrafeForce = 3f;
    public float minStrafeForce = 0.5f;
    
    [Header("Gizmo Settings")]
    public bool showGizmos = true;             // Toggle gizmo display
    public float gizmoScale = 2f;              // Scale of gizmo arrows

    [SerializeField] private ParticleSystem[] thrustParticles;

    private Rigidbody rb;
    private float currentRotationSpeed;
    private Quaternion rotation;
    private float thrustInput;
    private float strafeInput;
    private bool rotateToTarget;
    private Vector3 targetPosition;
    
    // Gizmo visualization data
    private Vector3 currentThrustVector;
    private Vector3 currentStrafeVector;
    private Vector3 targetDirection;
    private bool showTargetDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.drag = 0.1f;
        rb.useGravity = false;
        currentRotationSpeed = 0f;
        rotation = transform.rotation;
    }

    public void SetControls(float thrust, float strafe)
    {
        thrustInput = thrust;
        strafeInput = strafe;
    }

    public void SetRotationTarget(bool shouldRotate, Vector3 target)
    {
        rotateToTarget = shouldRotate;
        targetPosition = target;
    }

    private void FixedUpdate()
    {
        ApplyThrust();
        ApplyStrafeAndBank();
        ApplyRotation();

        // Final updates
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);
        localVelocity.z = 0; // Zero out forward movement in local space
        rb.velocity = transform.TransformDirection(localVelocity);
        
        // Apply the final rotation
        transform.rotation = rotation;
    }

    private void ApplyThrust()
    {
        Vector3 thrustDirection = transform.up * thrustInput * thrustForce;
        rb.AddForce(thrustDirection);
        
        // Store for gizmo display
        currentThrustVector = thrustDirection;

        if (thrustInput > 0.01f)
        {
            foreach (ParticleSystem particle in thrustParticles)
            {
                if (!particle.isPlaying) particle.Play();
            }
        }
        else
        {
            foreach (ParticleSystem particle in thrustParticles)
            {
                if (particle.isPlaying) particle.Stop();
            }
        }

        if (rb.velocity.magnitude > maxSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }
    }

    private void ApplyStrafeAndBank()
    {
        bool isBanking = Mathf.Abs(strafeInput) > 0.1f;
        float targetBankAngle = isBanking ? strafeInput * maxBankAngle : 0f;

        rotation = Quaternion.RotateTowards(rotation, Quaternion.AngleAxis(targetBankAngle, transform.up), bankingSpeed * Time.fixedDeltaTime);

        float speedRatio = rb.velocity.magnitude / maxSpeed;
        float strafeForce = Mathf.Lerp(maxStrafeForce, minStrafeForce, speedRatio);

        Vector3 strafeDirection = transform.right * strafeInput * strafeForce;
        rb.AddForce(strafeDirection, ForceMode.Force);
        
        // Store for gizmo display
        currentStrafeVector = strafeDirection;
    }

    private void ApplyRotation()
    {
        showTargetDirection = rotateToTarget;
        
        if (rotateToTarget)
        {
            // Store target direction for gizmo display
            targetDirection = (targetPosition - transform.position).normalized;
            
            // Convert target position to local space
            Vector3 localTarget = transform.InverseTransformPoint(targetPosition);
            float targetAngle = Mathf.Atan2(localTarget.x, localTarget.y) * Mathf.Rad2Deg-90f;
            float currentAngle = 0f; // In local space, the ship's forward is always 0
            float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);

            if (Mathf.Abs(angleDifference) > yawDeadzoneAngle)
            {
                float angleRatio = Mathf.Abs(angleDifference) / 180f;
                float thrustMultiplier = Mathf.Pow(angleRatio + 0.01f, 1 / 6f);
                currentRotationSpeed += Mathf.Sign(angleDifference) * rotationThrustForce * thrustMultiplier * Time.fixedDeltaTime;
            }
        }

        currentRotationSpeed *= rotationDrag;
        currentRotationSpeed = Mathf.Clamp(currentRotationSpeed, -maxRotationSpeed, maxRotationSpeed);
        rotation *= Quaternion.AngleAxis(currentRotationSpeed * Time.fixedDeltaTime, transform.forward);
    }

        private void OnDrawGizmos()
    {
        if (!showGizmos || !Application.isPlaying) return;
        
        Vector3 position = transform.position;
        
        // Draw roll axis (local forward - blue line with sphere)
        Gizmos.color = Color.blue;
        Vector3 rollAxis = transform.TransformDirection(Vector3.forward) * gizmoScale;
        Gizmos.DrawRay(position, rollAxis);
        Gizmos.DrawWireSphere(position + rollAxis, 0.1f * gizmoScale);
        
        // Draw yaw axis (local up - green line with cube)
        Gizmos.color = Color.green;
        Vector3 yawAxis = transform.TransformDirection(Vector3.up) * gizmoScale;
        Gizmos.DrawRay(position, yawAxis);
        Gizmos.DrawWireCube(position + yawAxis, Vector3.one * 0.15f * gizmoScale);
        
        // Draw current thrust vector (red - thick line)
        if (Mathf.Abs(thrustInput) > 0.01f)
        {
            Gizmos.color = Color.red;
            Vector3 thrustVector = currentThrustVector.normalized * gizmoScale * 1.5f;
            Gizmos.DrawRay(position, thrustVector);
            // Draw thrust intensity with wire cube size
            float intensity = Mathf.Abs(thrustInput);
            Gizmos.DrawWireCube(position + thrustVector, Vector3.one * (0.2f + intensity * 0.3f) * gizmoScale);
        }
        
        // Draw strafe vector (cyan - side thrust)
        if (Mathf.Abs(strafeInput) > 0.01f)
        {
            Gizmos.color = Color.cyan;
            Vector3 strafeVector = currentStrafeVector.normalized * gizmoScale;
            Gizmos.DrawRay(position, strafeVector);
            Gizmos.DrawWireCube(position + strafeVector, Vector3.one * 0.1f * gizmoScale);
        }
        
        // Draw turning target direction (yellow - target line)
        if (showTargetDirection)
        {
            Gizmos.color = Color.yellow;
            Vector3 targetVector = targetDirection * gizmoScale * 2f;
            Gizmos.DrawRay(position, targetVector);
            Gizmos.DrawWireSphere(position + targetVector, 0.15f * gizmoScale);
            
            // Draw deadzone cone
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 forward = transform.TransformDirection(Vector3.up);
            float deadzoneRadius = Mathf.Tan(yawDeadzoneAngle * Mathf.Deg2Rad) * gizmoScale;
            // Simple deadzone representation with circles
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 offset = Quaternion.AngleAxis(angle, forward) * transform.TransformDirection(Vector3.right) * deadzoneRadius;
                Gizmos.DrawWireSphere(position + forward * gizmoScale + offset, 0.05f * gizmoScale);
            }
        }
        
        // Draw velocity vector (magenta - current movement)
        if (rb != null && rb.velocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.magenta;
            Vector3 velocityVector = rb.velocity.normalized * gizmoScale * 1.2f;
            Gizmos.DrawRay(position, velocityVector);
            Gizmos.DrawWireCube(position + velocityVector, Vector3.one * 0.08f * gizmoScale);
        }
        
        // Draw rotation speed indicator (white - spinning cube)
        if (Mathf.Abs(currentRotationSpeed) > 1f)
        {
            Gizmos.color = Color.white;
            float rotationIntensity = Mathf.Abs(currentRotationSpeed) / maxRotationSpeed;
            Vector3 rotCenter = position + Vector3.back * 0.5f * gizmoScale;
            Gizmos.DrawWireCube(rotCenter, Vector3.one * (0.1f + rotationIntensity * 0.2f) * gizmoScale);
        }
    }

} 