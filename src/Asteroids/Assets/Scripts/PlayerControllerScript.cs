using UnityEngine;

// Controls player movement and rotation.
public class PlayerController : MonoBehaviour
{
    public float thrustForce = 5.0f; // Force applied for acceleration
    public float rotationSpeed = 120.0f; // Rotation speed

    private Rigidbody2D rb; // Reference to player's Rigidbody.

    // Start is called before the first frame update
    private void Start()
    {
        rb = GetComponent<Rigidbody2D>(); // Access player's Rigidbody.
        rb.gravityScale = 0f; // Disable gravity
        rb.drag = 0.1f; // Set low drag for space-like movement
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

        // Rotate player based on horizontal input
        float turn = -Input.GetAxis("Horizontal") * rotationSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation + turn);
    }
}