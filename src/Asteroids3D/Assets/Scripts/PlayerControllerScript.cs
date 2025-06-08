using UnityEngine;

// Controls player movement and rotation.
public class PlayerController : MonoBehaviour
{
    [Header("Thrust Settings")]
    public float thrustForce = 5.0f;        // Force applied for acceleration
    public float maxSpeed = 10.0f;          // Maximum speed limit
    
    [Header("Rotation Settings")]
    public float rotationThrustForce = 200.0f;  // Force applied for rotation
    public float maxRotationSpeed = 180.0f;     // Maximum rotation speed (degrees per second)
    public float rotationDrag = 0.95f;          // How quickly rotation slows down (0-1)

    [Header("Banking Settings")]
    public float maxBankAngle = 45f;           // Maximum banking angle in degrees
    public float bankingSpeed = 5f;            // How quickly the ship banks
    public float maxStrafeForce = 3f;          // Maximum strafe force when stationary
    public float minStrafeForce = 0.5f;        // Minimum strafe force at max speed

    private Rigidbody rb;                // Reference to player's Rigidbody
    private float currentRotationSpeed;    // Current rotation speed
    private Quaternion q_bank;        // Current banking angle
    private Quaternion q_yaw;        // Current banking angle
    private Camera mainCamera;       // Reference to main camera

    // Start is called before the first frame update
    private void Start()
    {
        rb = GetComponent<Rigidbody>();  // Access player's Rigidbody
        rb.drag = 0.1f;                    // Set low drag for space-like movement
        rb.useGravity = false;            // Disable gravity for space movement
        currentRotationSpeed = 0f;         // Initialize rotation speed
        q_yaw = Quaternion.identity;
        q_bank = Quaternion.identity;
        mainCamera = Camera.main;
    }

    // Handle physics-based movement and rotation.
    private void FixedUpdate()
    {
        // Apply thrust force based on vertical input
        float thrust = Input.GetAxis("Vertical");
        Vector3 thrustDirection = transform.up * thrust * thrustForce;
        rb.AddForce(thrustDirection);

        // Limit maximum speed
        if (rb.velocity.magnitude > maxSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }

        // Get strafe input from A/D axis
        float strafeInput = Input.GetAxis("Horizontal");
        
        // Handle banking and strafing
        bool isBanking = Mathf.Abs(strafeInput) > 0.1f;
        float targetBankAngle = isBanking ? strafeInput * maxBankAngle : 0f;

        q_bank = Quaternion.Lerp(q_bank, Quaternion.AngleAxis(targetBankAngle, Vector3.up), bankingSpeed * Time.fixedDeltaTime);

        // Calculate strafe force based on velocity
        float speedRatio = rb.velocity.magnitude / maxSpeed;
        float strafeForce = Mathf.Lerp(maxStrafeForce, minStrafeForce, speedRatio);

        Vector3 strafeDirection = q_yaw * (Vector3.left * strafeInput * strafeForce);
        rb.AddForce(strafeDirection, ForceMode.Force);
        
        // Handle rotation based on direction input and mouse position
        if (Input.GetButton("Direction"))
        {
            // Get mouse position in world space
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = transform.position.z;
            
            // Calculate angle to mouse position
            Vector2 direction = mousePos - transform.position;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            
            // Calculate the shortest rotation to the target angle
            float currentAngle = transform.rotation.eulerAngles.z;
            float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);
            
            // Apply rotation thrust towards target angle
            currentRotationSpeed += Mathf.Sign(angleDifference) * rotationThrustForce * Time.fixedDeltaTime;
        }
        
        // Apply rotation drag
        currentRotationSpeed *= rotationDrag;
        
        // Clamp rotation speed
        currentRotationSpeed = Mathf.Clamp(currentRotationSpeed, -maxRotationSpeed, maxRotationSpeed);
        
        // Apply rotation
        q_yaw *= Quaternion.Euler(0, 0, currentRotationSpeed * Time.fixedDeltaTime);
        
        transform.rotation = q_yaw * q_bank;
        transform.position = new Vector3(transform.position.x, transform.position.y, 0);
    }
}
