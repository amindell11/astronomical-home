// This file contains movement and plane logic for ships.
// Damage and health are now handled by ShipHealth.

using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class ShipMovement : MonoBehaviour
{
    // -------- Nested 2-D movement model --------
    public class ShipMovement2D
    {
        private ShipMovement ship; // reference to outer class

        // 2-D state
        public Vector2 Position          { get; internal set; }
        public Vector2 Velocity          { get; internal set; }
        public Vector2 ForceVector       { get; private set; }
        public float   Angle             { get; internal set; } // degrees
        public float   AngularVelocity   { get; internal set; }

        // Input state
        public float ThrustInput   { get; internal set; }
        public float StrafeInput   { get; internal set; }
        public bool  RotateToTarget{ get; internal set; }
        public float TargetAngle   { get; internal set; }

        public ShipMovement2D(ShipMovement owner) => ship = owner;

        public void SetControls(float thrust, float strafe)
        {
            ThrustInput  = thrust;
            StrafeInput  = strafe;
        }
        public void SetRotationTarget(bool shouldRotate, float targetAngle)
        {
            RotateToTarget = shouldRotate;
            TargetAngle    = targetAngle;
        }
        public void Update(float dt)
        {
            ApplyYawRotation(dt);
            ApplyThrust(dt);
        }

        void ApplyYawRotation(float dt)
        {
            if (RotateToTarget)
            {
                float diff = Mathf.DeltaAngle(Angle, TargetAngle);
                if (Mathf.Abs(diff) > ship.yawDeadzoneAngle)
                {
                    float ratio   = Mathf.Abs(diff) / 180f;
                    float mult    = Mathf.Pow(ratio + 0.01f, 1f / 6f);
                    AngularVelocity += Mathf.Sign(diff) * ship.rotationThrustForce * mult * dt;
                }
            }
            AngularVelocity *= ship.rotationDrag;
            AngularVelocity  = Mathf.Clamp(AngularVelocity, -ship.maxRotationSpeed, ship.maxRotationSpeed);

            Angle += AngularVelocity * dt;
            if (Angle > 360) Angle -= 360;
            if (Angle < 0)   Angle += 360;
        }

        void ApplyThrust(float dt)
        {
            float   angRad   = Angle * Mathf.Deg2Rad;
            Vector2 forward  = new Vector2(-Mathf.Sin(angRad), Mathf.Cos(angRad));
            Vector2 thrustV  = forward * ThrustInput * ship.thrustForce;

            float speedRatio = Velocity.magnitude / ship.maxSpeed;
            float strafeF    = Mathf.Lerp(ship.maxStrafeForce, ship.minStrafeForce, speedRatio);
            Vector2 right    = new Vector2(forward.y, -forward.x);
            Vector2 strafeV  = right * StrafeInput * strafeF;

            ForceVector = thrustV + strafeV;
        }
    }

    // ------------- Inspector fields -------------
    [Header("Reference Plane")]
    [Tooltip("Transform that defines the plane the ship operates in. If null, uses world XY plane.")]
    public Transform referencePlane;

    [Header("Thrust Settings")]
    public float thrustForce = 1200f;
    public float maxSpeed    = 15f;

    [Header("Rotation Settings")]
    public float rotationThrustForce = 480f;
    public float maxRotationSpeed    = 180f;
    public float rotationDrag        = 0.95f;
    [Tooltip("The angle in degrees inside which the ship will not try to yaw towards the target.")]
    public float yawDeadzoneAngle    = 4f;

    [Header("Banking Settings")]
    public float maxBankAngle  = 45f;
    public float bankingSpeed  = 5f;
    public float maxStrafeForce= 800f;
    public float minStrafeForce= 750f;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    [Header("Effects")]
    [SerializeField] ParticleSystem[] thrustParticles;

    // ------------- Runtime cached fields -------------
    Rigidbody  rb;
    Quaternion q_bank = Quaternion.identity;
    Quaternion q_yaw  = Quaternion.identity;

    public ShipMovement2D Controller { get; private set; }

    Vector3 cachedPlaneOrigin;
    Vector3 cachedPlaneNormal;
    Vector3 cachedPlaneForward;
    Vector3 cachedPlaneRight;

    float maxSpeedSquared;

    // Damage flash has moved to ShipHealth

    // -------------------------------------------------
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.drag        = 0.2f;
        rb.angularDrag = 0f;
        rb.useGravity  = false;

        Controller = new ShipMovement2D(this);

        maxSpeedSquared = maxSpeed * maxSpeed;

        CachePlaneInfo();
        SyncAngleFrom3D();
    }

    void FixedUpdate()
    {
        SyncStateFrom3D();
        Controller.Update(Time.fixedDeltaTime);
        ApplyForces();
        ApplyRotation();
        ClampSpeed();
        ConstrainToPlane();
        UpdateThrustParticles();
    }

    // ----- Movement helpers (Sync, Apply, etc.) --------------------------
    void SyncStateFrom3D()
    {
        Vector3 planeNormal = cachedPlaneNormal;
        Vector3 planePos3D  = Vector3.ProjectOnPlane(transform.position - cachedPlaneOrigin, planeNormal);
        Controller.Position = WorldToPlane(planePos3D);
        Controller.Velocity = WorldToPlane(rb.velocity);
    }
    void SyncAngleFrom3D()
    {
        Vector3 planeNormal      = cachedPlaneNormal;
        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.up, planeNormal).normalized;
        if (projectedForward.sqrMagnitude > 0.01f)
        {
            float ang = Vector3.SignedAngle(cachedPlaneForward, projectedForward, planeNormal);
            Controller.Angle = ang < 0 ? ang + 360f : ang;
        }
    }
    void ApplyForces()   => rb.AddForce(PlaneToWorld(Controller.ForceVector));
    void ApplyRotation()
    {
        q_yaw  = Quaternion.AngleAxis(Controller.Angle, Vector3.forward);
        float targetBank = -Controller.StrafeInput * maxBankAngle;
        Quaternion q_targetBank = Quaternion.AngleAxis(targetBank, Vector3.up);
        q_bank = Quaternion.Lerp(q_bank, q_targetBank, bankingSpeed * Time.fixedDeltaTime);
        transform.rotation = (referencePlane ? referencePlane.rotation : Quaternion.identity) * q_yaw * q_bank;
    }
    void ClampSpeed()
    {
        if (rb.velocity.sqrMagnitude > maxSpeedSquared) rb.velocity = rb.velocity.normalized * maxSpeed;
    }
    void ConstrainToPlane()
    {
        Vector3 n = cachedPlaneNormal;
        float d   = Vector3.Dot(transform.position - cachedPlaneOrigin, n);
        transform.position -= n * d;
        rb.velocity = Vector3.ProjectOnPlane(rb.velocity, n);
    }

    void UpdateThrustParticles()
    {
        if (thrustParticles == null || thrustParticles.Length == 0) return;
        bool shouldPlay = Controller.ThrustInput > 0.05f;
        foreach (var ps in thrustParticles)
        {
            if (!ps) continue;
            if (shouldPlay)
            {
                if (!ps.isPlaying) ps.Play(true);
            }
            else
            {
                if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    // ----- Coordinate helpers -------------------------------------------
    public Vector3 GetPlaneOrigin()  => referencePlane ? referencePlane.position : Vector3.zero;
    public Vector3 GetPlaneForward() => referencePlane ? referencePlane.up       : Vector3.up;
    public Vector3 GetPlaneRight()   => referencePlane ? referencePlane.right    : Vector3.right;
    public Vector3 GetPlaneNormal()  => referencePlane ? referencePlane.forward  : Vector3.forward;

    public Vector2 WorldToPlane(Vector3 w)
    {
        return new Vector2(Vector3.Dot(w, cachedPlaneRight), Vector3.Dot(w, cachedPlaneForward));
    }
    public Vector3 PlaneToWorld(Vector2 p)
    {
        return cachedPlaneRight * p.x + cachedPlaneForward * p.y;
    }

    void CachePlaneInfo()
    {
        cachedPlaneOrigin  = GetPlaneOrigin();
        cachedPlaneNormal  = GetPlaneNormal();
        cachedPlaneForward = GetPlaneForward();
        cachedPlaneRight   = GetPlaneRight();
    }

    // ---------- Utility API ----------
    public void ResetShip()
    {
        rb.velocity        = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Controller.Velocity = Vector2.zero;
    }

    public void TriggerDamageFlash() { /* Flash handled by ShipHealth.*/ }
} 