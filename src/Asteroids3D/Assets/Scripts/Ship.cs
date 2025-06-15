using UnityEngine;

// The main MonoBehaviour class for the ship. It handles all interaction with the
// Unity 3D environment (Rigidbody, Transforms, etc.). It wraps a pure-logic Ship2D
// class that performs all the 2D calculations.
[RequireComponent(typeof(Rigidbody))]
public class Ship : MonoBehaviour, IDamageable
{
    // The inner class that handles all the 2D ship logic. It knows nothing
    // about 3D space, GameObjects, or Unity's physics engine.
    public class Ship2D
    {
        private Ship ship; // Reference to the outer class for parameters

        // 2D State
        public Vector2 Position { get; internal set; }
        public Vector2 Velocity { get; internal set; }
        public Vector2 ForceVector { get; private set; }
        public float Angle { get; internal set; } // In degrees
        public float AngularVelocity { get; internal set; }

        // Input State
        public float ThrustInput { get; internal set; }
        public float StrafeInput { get; internal set; }
        public bool RotateToTarget { get; internal set; }
        public float TargetAngle { get; internal set; }

        public Ship2D(Ship owner)
        {
            ship = owner;
        }

        public void SetControls(float thrust, float strafe)
        {
            ThrustInput = thrust;
            StrafeInput = strafe;
        }

        public void SetRotationTarget(bool shouldRotate, float targetAngle)
        {
            RotateToTarget = shouldRotate;
            TargetAngle = targetAngle;
        }

        public void Update(float deltaTime)
        {
            ApplyYawRotation(deltaTime);
            ApplyThrust(deltaTime);
        }

        private void ApplyYawRotation(float deltaTime)
        {
            if (RotateToTarget)
            {
                float angleDifference = Mathf.DeltaAngle(Angle, TargetAngle);
                if (Mathf.Abs(angleDifference) > ship.yawDeadzoneAngle)
                {
                    float angleRatio = Mathf.Abs(angleDifference) / 180f;
                    float thrustMultiplier = Mathf.Pow(angleRatio + 0.01f, 1f / 6f);
                    AngularVelocity += Mathf.Sign(angleDifference) * ship.rotationThrustForce * thrustMultiplier * deltaTime;
                }
            }
            
            AngularVelocity *= ship.rotationDrag;
            AngularVelocity = Mathf.Clamp(AngularVelocity, -ship.maxRotationSpeed, ship.maxRotationSpeed);

            Angle += AngularVelocity * deltaTime;
            if (Angle > 360) Angle -= 360;
            if (Angle < 0) Angle += 360;
        }

        private void ApplyThrust(float deltaTime)
        {
            float angleRad = Angle * Mathf.Deg2Rad;
            // The forward direction in a 2D plane, where the angle is measured counter-clockwise from the positive Y axis,
            // is (-sin(angle), cos(angle)).
            Vector2 forwardDirection = new Vector2(-Mathf.Sin(angleRad), Mathf.Cos(angleRad));
            
            Vector2 thrustVector = forwardDirection * ThrustInput * ship.thrustForce;
            
            float speedRatio = Velocity.magnitude / ship.maxSpeed;
            float strafeForce = Mathf.Lerp(ship.maxStrafeForce, ship.minStrafeForce, speedRatio);
            Vector2 rightDirection = new Vector2(forwardDirection.y, -forwardDirection.x);
            Vector2 strafeVector = rightDirection * StrafeInput * strafeForce;
            
            ForceVector = thrustVector + strafeVector;
        }
    }

    [Header("Reference Plane")]
    [Tooltip("Transform that defines the plane the ship operates in. If null, uses world XY plane.")]
    public Transform referencePlane;
    
    [Header("Thrust Settings")]
    public float thrustForce = 1200.0f;
    public float maxSpeed = 15.0f;

    [Header("Rotation Settings")]
    public float rotationThrustForce = 480.0f;
    public float maxRotationSpeed = 180.0f;
    public float rotationDrag = 0.95f;
    [Tooltip("The angle in degrees inside which the ship will not try to yaw towards the target.")]
    public float yawDeadzoneAngle = 4.0f;

    [Header("Banking Settings")]
    public float maxBankAngle = 45f;
    public float bankingSpeed = 5f;
    public float maxStrafeForce = 800f;
    public float minStrafeForce = 750f;
    
    [Header("Debug")]
    public bool enableDebugLogs = false;

    [SerializeField] private ParticleSystem[] thrustParticles;

    private Rigidbody rb;
    private Quaternion q_bank = Quaternion.identity;
    private Quaternion q_yaw = Quaternion.identity;
    public Ship2D Controller { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.drag = 0.2f;
        rb.angularDrag = 0f;
        rb.useGravity = false;
        Controller = new Ship2D(this);
        SyncAngleFrom3D(); // Sync only once on startup
    }

    private void FixedUpdate()
    {
        // Sync necessary state from 3D world to 2D controller
        SyncStateFrom3D();

        // Update the 2D controller logic
        Controller.Update(Time.fixedDeltaTime);

        // Apply results from controller to the 3D Rigidbody
        ApplyForces();
        ApplyRotation();
        ClampSpeed();
        ConstrainToPlane();
    }
    
    private void SyncStateFrom3D()
    {
        Vector3 planeNormal = GetPlaneNormal();

        // Position
        Vector3 planePos3D = Vector3.ProjectOnPlane(transform.position - GetPlaneOrigin(), planeNormal);
        Controller.Position = WorldToPlane(planePos3D);

        // Velocity
        Controller.Velocity = WorldToPlane(rb.velocity);
    }
    
    private void SyncAngleFrom3D()
    {
        Vector3 planeNormal = GetPlaneNormal();
        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.up, planeNormal).normalized;
        if (projectedForward.sqrMagnitude > 0.01f)
        {
            float angle = Vector3.SignedAngle(GetPlaneForward(), projectedForward, planeNormal);
            Controller.Angle = angle < 0 ? angle + 360f : angle;
        }
    }
    
    private void ApplyForces()
    {
        Vector3 force = PlaneToWorld(Controller.ForceVector);
        rb.AddForce(force);
    }

    private void ApplyRotation()
    {
        // Yaw should be calculated around the local 'up' axis of the plane, which corresponds to Vector3.forward.
        q_yaw = Quaternion.AngleAxis(Controller.Angle, Vector3.forward);
        
        // Banking (visual only) is a roll around the ship's local forward axis (Vector3.up in the plane's space).
        float targetBankAngle = -Controller.StrafeInput * maxBankAngle;
        Quaternion targetBankRotation = Quaternion.AngleAxis(targetBankAngle, Vector3.up);
        q_bank = Quaternion.Lerp(q_bank, targetBankRotation, bankingSpeed * Time.fixedDeltaTime);
        
        // Combine the rotations: start with the plane's orientation, then apply the local yaw and bank.
        transform.rotation = (referencePlane != null ? referencePlane.rotation : Quaternion.identity) * q_yaw * q_bank;
    }

    private void ClampSpeed()
    {
        if (rb.velocity.magnitude > maxSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }
    }

    private void ConstrainToPlane()
    {
        Vector3 planeNormal = GetPlaneNormal();

        // Lock position to plane
        Vector3 pointOnPlane = GetPlaneOrigin();
        Vector3 vectorFromPlane = transform.position - pointOnPlane;
        float distance = Vector3.Dot(vectorFromPlane, planeNormal);
        transform.position -= planeNormal * distance;

        // Zero out velocity component perpendicular to plane
        rb.velocity = Vector3.ProjectOnPlane(rb.velocity, planeNormal);
    }

    #region Coordinate System Helpers

    public Vector3 GetPlaneOrigin() => referencePlane != null ? referencePlane.position : Vector3.zero;
    public Vector3 GetPlaneForward() => referencePlane != null ? referencePlane.up : Vector3.up;
    public Vector3 GetPlaneRight() => referencePlane != null ? referencePlane.right : Vector3.right;
    public Vector3 GetPlaneNormal() => referencePlane != null ? referencePlane.forward : Vector3.forward;

    public Vector2 WorldToPlane(Vector3 worldVector)
    {
        Vector3 planeForward = GetPlaneForward();
        Vector3 planeRight = GetPlaneRight();
        float y = Vector3.Dot(worldVector, planeForward);
        float x = Vector3.Dot(worldVector, planeRight);
        return new Vector2(x, y);
    }
    
    public Vector3 PlaneToWorld(Vector2 planeVector)
    {
        Vector3 planeForward = GetPlaneForward();
        Vector3 planeRight = GetPlaneRight();
        return planeRight * planeVector.x + planeForward * planeVector.y;
    }

    #endregion

    #region IDamageable Implementation

    [Header("Damage Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private float explosionVolume = 0.7f;
    [SerializeField] private bool isPlayerShip = false;
    
    private float currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint)
    {
        currentHealth -= damage;
        
        // Apply physics impact
        Vector3 impactForce = projectileVelocity.normalized * (projectileMass * projectileVelocity.magnitude * 0.1f);
        rb.AddForceAtPosition(impactForce, hitPoint, ForceMode.Impulse);
        
        Debug.Log($"Ship took {damage} damage. Health: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            DestroyShip();
        }
    }

    private void DestroyShip()
    {
        // Spawn explosion effect
        if (explosionPrefab != null)
        {
            // Try to get PooledVFX component first, fallback to regular instantiate
            PooledVFX pooledVFX = explosionPrefab.GetComponent<PooledVFX>();
            if (pooledVFX != null)
            {
                SimplePool<PooledVFX>.Get(pooledVFX, transform.position, Quaternion.identity);
            }
            else
            {
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }
        }
        
        // Play explosion sound
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
        }
        
        if (isPlayerShip)
        {
            // Inform GameManager of player death
            GameManager.Instance?.HandlePlayerDeath();
        }
        else
        {
            // Additional AI death behavior could go here (e.g., spawn loot)
        }
        
        // Disable the ship object
        gameObject.SetActive(false);
    }

    #endregion
} 