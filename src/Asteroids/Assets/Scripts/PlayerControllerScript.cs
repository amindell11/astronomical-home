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

    private Rigidbody2D rb;                // Reference to player's Rigidbody
    private float currentRotationSpeed;    // Current rotation speed

    // Start is called before the first frame update
    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();  // Access player's Rigidbody
        rb.gravityScale = 0f;              // Disable gravity
        rb.drag = 0.1f;                    // Set low drag for space-like movement
        currentRotationSpeed = 0f;         // Initialize rotation speed
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // Handle physics-based movement and rotation.
    private void FixedUpdate()
    {
        // Apply thrust force based on vertical input
        float thrust = Input.GetAxis("Vertical");
        Vector2 thrustDirection = transform.up * thrust * thrustForce;
        rb.AddForce(thrustDirection);

        // Limit maximum speed
        if (rb.velocity.magnitude > maxSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }

        // Handle rotation with thrust
        float turnInput = -Input.GetAxis("Horizontal");
        
        // Apply rotation thrust
        currentRotationSpeed += turnInput * rotationThrustForce * Time.fixedDeltaTime;
        
        // Apply rotation drag
        currentRotationSpeed *= rotationDrag;
        
        // Clamp rotation speed
        currentRotationSpeed = Mathf.Clamp(currentRotationSpeed, -maxRotationSpeed, maxRotationSpeed);
        
        // Apply rotation
        rb.MoveRotation(rb.rotation + currentRotationSpeed * Time.fixedDeltaTime);
    }
}