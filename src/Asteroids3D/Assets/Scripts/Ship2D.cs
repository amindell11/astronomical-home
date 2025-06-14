using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Ship2D : MonoBehaviour
{
    [Header("Reference Plane")]
    [Tooltip("Transform that defines the plane the ship operates in. If null, uses world XY plane.")]
    public Transform referencePlane;
    
    [Header("Thrust Settings")]
    public float thrustForce = 5.0f;
    public float maxSpeed = 10.0f;

    [Header("Rotation Settings")]
    public float rotationThrustForce = 200.0f;
    public float maxRotationSpeed = 180.0f;
    public float rotationDrag = 0.95f;
    [Tooltip("The angle in degrees inside which the ship will not try to yaw towards the target.")]
    public float yawDeadzoneAngle = 2.0f;

    [Header("Banking Settings")]
    public float maxBankAngle = 45f;
    public float bankingSpeed = 5f;
    public float maxStrafeForce = 3f;
    public float minStrafeForce = 0.5f;
    
    [Header("Debug")]
    public bool enableDebugLogs = false;

    [SerializeField] private ParticleSystem[] thrustParticles;

    private Rigidbody rb;
    private float currentRotationSpeed;
    private Quaternion q_yaw;        // Current yaw quaternion
    private Quaternion q_bank;       // Current banking quaternion
    
    // Input values
    private float thrustInput;
    private float strafeInput;
    private bool rotateToTarget;
    private float targetYaw;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.drag = 0.1f;
        rb.useGravity = false;
        currentRotationSpeed = 0f;
        q_yaw = Quaternion.identity;
        q_bank = Quaternion.identity;
    }

    public void SetControls(float thrust, float strafe)
    {
        thrustInput = thrust;
        strafeInput = strafe;
        
        if (enableDebugLogs)
            Debug.Log($"Ship2D Controls - Thrust: {thrust}, Strafe: {strafe}");
    }

    public void SetRotationTarget(bool shouldRotate, float targetYawAngle)
    {
        rotateToTarget = shouldRotate;
        targetYaw = targetYawAngle;
        
        if (enableDebugLogs)
            Debug.Log($"Ship2D Rotation - Should rotate: {shouldRotate}, Target yaw: {targetYawAngle}");
    }

    public void SetRotationTarget(bool shouldRotate, Vector3 targetPosition)
    {
        rotateToTarget = shouldRotate;
        
        if (shouldRotate)
        {
            // Calculate direction to target position
            Vector3 directionToTarget = (targetPosition - transform.position).normalized;
            
            // Project direction onto the reference plane
            Vector3 planeNormal = GetPlaneNormal();
            Vector3 projectedDirection = Vector3.ProjectOnPlane(directionToTarget, planeNormal).normalized;
            
            // Calculate angle from plane forward direction
            Vector3 planeForward = GetPlaneForward();
            float angle = Vector3.SignedAngle(planeForward, projectedDirection, planeNormal);
            
            // Convert to 0-360 range
            targetYaw = angle < 0 ? angle + 360f : angle;
            
            if (enableDebugLogs)
                Debug.Log($"Ship2D Rotation - Target position: {targetPosition}, Direction: {directionToTarget}, Projected: {projectedDirection}, Target yaw: {targetYaw}");
        }
        else
        {
            targetYaw = 0f;
        }
    }

    private void FixedUpdate()
    {
        ApplyThrust();
        ApplyStrafeAndBanking();
        ApplyYawRotation();
        UpdateTransform();
        ConstrainToPlane();
    }

    private void ApplyThrust()
    {
        Vector3 thrustDirection = transform.up * thrustInput * thrustForce;
        rb.AddForce(thrustDirection);

        // Handle thrust particles
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

        // Limit maximum speed
        if (rb.velocity.magnitude > maxSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }
        
        if (enableDebugLogs && Mathf.Abs(thrustInput) > 0.01f)
            Debug.Log($"Thrust applied - Direction: {thrustDirection.normalized}, Magnitude: {thrustDirection.magnitude}");
    }

    private void ApplyStrafeAndBanking()
    {
        // Handle banking (roll) - using quaternion lerp like original
        bool isBanking = Mathf.Abs(strafeInput) > 0.1f;
        float targetBankAngle = isBanking ? -strafeInput * maxBankAngle : 0f;
        
        Quaternion targetBankRotation = Quaternion.AngleAxis(targetBankAngle, Vector3.up);
        q_bank = Quaternion.Lerp(q_bank, targetBankRotation, bankingSpeed * Time.fixedDeltaTime);
        
        if (enableDebugLogs && isBanking)
            Debug.Log($"Banking - Strafe input: {strafeInput}, Target bank angle: {targetBankAngle}");

        // Handle strafe force
        float speedRatio = rb.velocity.magnitude / maxSpeed;
        float strafeForce = Mathf.Lerp(maxStrafeForce, minStrafeForce, speedRatio);

        Vector3 strafeDirection = GetPlaneRight() * strafeInput * strafeForce;
        rb.AddForce(strafeDirection, ForceMode.Force);
        
        if (enableDebugLogs && Mathf.Abs(strafeInput) > 0.01f)
            Debug.Log($"Strafe applied - Direction: {strafeDirection.normalized}, Force: {strafeForce}");
    }

    private void ApplyYawRotation()
    {
        if (rotateToTarget)
        {
            // Get current yaw angle from quaternion
            float currentYaw = GetYawAngle();
            float angleDifference = Mathf.DeltaAngle(currentYaw, targetYaw);
            
            if (Mathf.Abs(angleDifference) > yawDeadzoneAngle)
            {
                float angleRatio = Mathf.Abs(angleDifference) / 180f;
                float thrustMultiplier = Mathf.Pow(angleRatio + 0.01f, 1f / 6f);
                currentRotationSpeed += Mathf.Sign(angleDifference) * rotationThrustForce * thrustMultiplier * Time.fixedDeltaTime;
                
                if (enableDebugLogs)
                    Debug.Log($"Yaw rotation - Current: {currentYaw}, Target: {targetYaw}, Difference: {angleDifference}, Speed: {currentRotationSpeed}");
            }
        }

        // Apply rotation drag and limits
        currentRotationSpeed *= rotationDrag;
        currentRotationSpeed = Mathf.Clamp(currentRotationSpeed, -maxRotationSpeed, maxRotationSpeed);
        
        // Update yaw quaternion - always around local Z axis (plane normal in local space)
        q_yaw *= Quaternion.AngleAxis(currentRotationSpeed * Time.fixedDeltaTime, Vector3.forward);
    }

    private void UpdateTransform()
    {
        // Combine quaternions like original PlayerControllerScript
        if (referencePlane != null)
        {
            // Apply ship's local rotation (yaw * bank) relative to the reference plane
            transform.rotation = referencePlane.rotation * q_yaw * q_bank;
        }
        else
        {
            // Default to world XY plane
            transform.rotation = q_yaw * q_bank;
        }
    }

    private void ConstrainToPlane()
    {
        if (referencePlane != null)
        {
            // Project position onto the reference plane
            Vector3 planePosition = Vector3.ProjectOnPlane(transform.position - referencePlane.position, GetPlaneNormal()) + referencePlane.position;
            transform.position = planePosition;
        }
        else
        {
            // Constrain to XY plane (Z = 0)
            transform.position = new Vector3(transform.position.x, transform.position.y, 0);
        }
    }

    // Helper methods to get plane directions
    public Vector3 GetPlaneForward()
    {
        if (referencePlane != null)
            return referencePlane.up; // Assuming plane's up is the forward direction for the ship
        else
            return Vector3.up; // World Y is forward
    }

    public Vector3 GetPlaneRight()
    {
        if (referencePlane != null)
            return referencePlane.right;
        else
            return Vector3.right; // World X is right
    }

    public Vector3 GetPlaneNormal()
    {
        if (referencePlane != null)
            return referencePlane.forward; // Normal to the plane
        else
            return Vector3.forward; // World Z is normal to XY plane
    }

    // Helper method to extract yaw angle from quaternion
    private float GetYawAngle()
    {
        // Extract yaw angle from the quaternion in local space
        Vector3 forward = q_yaw * Vector3.up; // Local forward is Y
        float angle = Vector3.SignedAngle(Vector3.up, forward, Vector3.forward);
        return angle < 0 ? angle + 360f : angle;
    }

    // Public getters for debugging/gizmos
    public float CurrentYaw => GetYawAngle();
    public float CurrentRoll 
    { 
        get 
        {
            return Quaternion.Angle(q_bank, Quaternion.identity);
        }
    }
    public float TargetYaw => targetYaw;
    public bool IsRotatingToTarget => rotateToTarget;
    public Vector3 CurrentVelocity => rb.velocity;

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        Vector3 position = transform.position;
        float gizmoScale = 3f;
        
        // Draw current velocity vector (magenta)
        if (rb != null && rb.velocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.magenta;
            Vector3 velocityVector = rb.velocity.normalized * gizmoScale;
            Gizmos.DrawRay(position, velocityVector);
            Gizmos.DrawWireCube(position + velocityVector, Vector3.one * 0.1f);
            
            // Draw velocity magnitude as text would be nice, but we can show it with sphere size
            float velocityMagnitude = rb.velocity.magnitude;
            Gizmos.DrawWireSphere(position + velocityVector, 0.05f + velocityMagnitude * 0.02f);
        }
        
        // Draw rotation target direction (yellow)
        if (rotateToTarget)
        {
            // Calculate target direction from target yaw angle
            Vector3 planeForward = GetPlaneForward();
            Vector3 planeNormal = GetPlaneNormal();
            
            // Create target direction from yaw angle
            Quaternion targetRotation = Quaternion.AngleAxis(targetYaw, planeNormal);
            Vector3 targetDirection = targetRotation * planeForward;
            
            // Apply reference plane rotation if it exists
            if (referencePlane != null)
            {
                targetDirection = referencePlane.rotation * Quaternion.Inverse(referencePlane.rotation) * targetDirection;
            }
            
            Gizmos.color = Color.yellow;
            Vector3 targetVector = targetDirection * gizmoScale * 1.5f;
            Gizmos.DrawRay(position, targetVector);
            Gizmos.DrawWireSphere(position + targetVector, 0.15f);
        }
        
        // Draw current forward direction (green)
        Gizmos.color = Color.green;
        Vector3 forwardVector = transform.up * gizmoScale;
        Gizmos.DrawRay(position, forwardVector);
        Gizmos.DrawWireCube(position + forwardVector, Vector3.one * 0.08f);
        
        // Draw current right direction for reference (cyan)
        Gizmos.color = Color.cyan;
        Vector3 rightVector = transform.right * gizmoScale * 0.7f;
        Gizmos.DrawRay(position, rightVector);
        Gizmos.DrawWireCube(position + rightVector, Vector3.one * 0.06f);
        
        // Draw plane normal if using reference plane (blue)
        if (referencePlane != null)
        {
            Gizmos.color = Color.blue;
            Vector3 normalVector = GetPlaneNormal() * gizmoScale * 0.5f;
            Gizmos.DrawRay(position, normalVector);
            Gizmos.DrawWireCube(position + normalVector, Vector3.one * 0.04f);
        }
        
        // Draw thrust and strafe forces if active
        if (Mathf.Abs(thrustInput) > 0.01f)
        {
            Gizmos.color = Color.red;
            Vector3 thrustVector = transform.up * thrustInput * gizmoScale * 0.8f;
            Gizmos.DrawRay(position, thrustVector);
            float intensity = Mathf.Abs(thrustInput);
            Gizmos.DrawWireCube(position + thrustVector, Vector3.one * (0.05f + intensity * 0.1f));
        }
        
        if (Mathf.Abs(strafeInput) > 0.01f)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.8f); // Semi-transparent cyan
            Vector3 strafeVector = transform.right * strafeInput * gizmoScale * 0.6f;
            Gizmos.DrawRay(position, strafeVector);
            Gizmos.DrawWireCube(position + strafeVector, Vector3.one * 0.05f);
        }
    }
} 