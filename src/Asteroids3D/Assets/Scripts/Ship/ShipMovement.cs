// This file contains movement and plane logic for ships.
// Damage and health are now handled by ShipHealth.

using UnityEngine;
using System.Collections;
using UnityEngine.Serialization;

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
            float thrustForce = ThrustInput >= 0 ? ship.forwardThrustForce : ship.reverseThrustForce;
            Vector2 thrustV  = forward * ThrustInput * thrustForce;

            float speedRatio = Velocity.magnitude / ship.maxSpeed;
            float strafeF    = Mathf.Lerp(ship.maxStrafeForce, ship.minStrafeForce, speedRatio);
            Vector2 right    = new Vector2(forward.y, -forward.x);
            Vector2 strafeV  = right * StrafeInput * strafeF;

            ForceVector = thrustV + strafeV;
        }
    }
    public float maxSpeed{get; private set;}
    public float maxRotationSpeed;
    public float forwardThrustForce;
    public float reverseThrustForce;
    public float maxStrafeForce;
    public float minStrafeForce;
    public float rotationThrustForce;
    public float rotationDrag;
    public float yawDeadzoneAngle;
    public float maxBankAngle;
    public float bankingSpeed;
    public Transform referencePlane;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    [Header("Movement Gizmos")]
    public bool showMovementGizmos = true;
    public float movementGizmoScale = 3f;

    // ------------- Runtime cached fields -------------
    Rigidbody  rb;
    Quaternion q_bank = Quaternion.identity;
    Quaternion q_yaw  = Quaternion.identity;

    public ShipMovement2D Controller { get; private set; }


    // Latest kinematics snapshot
    ShipKinematics _kin;
    public ShipKinematics Kinematics => _kin;

    // Damage flash has moved to ShipHealth

    // -------------------------------------------------
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping        = 0.2f;
        rb.angularDamping = 0f;
        rb.useGravity  = false;

        Controller = new ShipMovement2D(this);
        referencePlane = GamePlane.Plane;
        SyncAngleFrom3D();
    }

    void Start()
    {
        rb.maxLinearVelocity = maxSpeed;
    }

    void FixedUpdate()
    {
        SyncStateFrom3D();
        Controller.Update(Time.fixedDeltaTime);
        ApplyForces();
        ApplyRotation();
        ConstrainToPlane();

        // Refresh kinematics snapshot for external consumers
        _kin = new ShipKinematics(Controller.Position, Controller.Velocity, Controller.Angle);
    }

    // ----- Movement helpers (Sync, Apply, etc.) --------------------------
    void SyncStateFrom3D()
    {
        Controller.Position = GamePlane.WorldToPlane(transform.position);
        Controller.Velocity = GamePlane.WorldToPlane(rb.linearVelocity);
    }
    void SyncAngleFrom3D()
    {
        Vector3 planeNormal      = GamePlane.Normal;
        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.up, planeNormal).normalized;
        if (projectedForward.sqrMagnitude > 0.01f)
        {
            float ang = Vector3.SignedAngle(GamePlane.Forward, projectedForward, planeNormal);
            Controller.Angle = ang < 0 ? ang + 360f : ang;
        }
    }
    void ApplyForces()   => rb.AddForce(GamePlane.PlaneVectorToWorld(Controller.ForceVector));
    void ApplyRotation()
    {
        q_yaw  = Quaternion.AngleAxis(Controller.Angle, Vector3.forward);
        float targetBank = -Controller.StrafeInput * maxBankAngle;
        Quaternion q_targetBank = Quaternion.AngleAxis(targetBank, Vector3.up);
        q_bank = Quaternion.Lerp(q_bank, q_targetBank, bankingSpeed * Time.fixedDeltaTime);
        transform.rotation = (referencePlane ? referencePlane.rotation : Quaternion.identity) * q_yaw * q_bank;
    }
    void ConstrainToPlane()
    {
        Vector3 n = GamePlane.Normal;
        float d   = Vector3.Dot(transform.position - GamePlane.Origin, n);
        transform.position -= n * d;
        rb.linearVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, n);
    }
    // ---------- Utility API ----------
    public void ResetShip()
    {
        rb.linearVelocity        = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Controller.Velocity = Vector2.zero;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showMovementGizmos || Controller == null) return;

        Vector3 pos   = transform.position;
        float   scale = movementGizmoScale;

        // Single color for all raw movement gizmos
        Gizmos.color = Color.yellow;

        // Thrust vector (sphere head)
        Vector3 thrustVec = transform.up * Controller.ThrustInput * scale;
        Gizmos.DrawLine(pos, pos + thrustVec);
        Gizmos.DrawSphere(pos + thrustVec, 0.15f);

        // Strafe vector (cube head)
        Vector3 strafeVec = transform.right * Controller.StrafeInput * scale;
        Gizmos.DrawLine(pos, pos + strafeVec);
        Gizmos.DrawCube(pos + strafeVec, Vector3.one * 0.25f);
    }
#endif

    // ---------------- 2-D Kinematics helpers (guidance pipeline) ----------------
    public Vector2 Position2D => Controller != null ? Controller.Position : Vector2.zero;
    public Vector2 Velocity2D => Controller != null ? Controller.Velocity : Vector2.zero;
    public Vector2 Forward2D
    {
        get
        {
            float a = Controller != null ? Controller.Angle * Mathf.Deg2Rad : 0f;
            return new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
        }
    }

    // ---------------- Settings API ----------------
    /// <summary>
    /// Updates this movement component's tunable parameters from a ShipSettings asset.
    /// Call once at startup or whenever the asset changes.
    /// </summary>
    public void ApplySettings(ShipSettings s)
    {
        if (s == null) return;

        forwardThrustForce   = s.forwardAcceleration;
        reverseThrustForce   = s.reverseAcceleration;
        maxSpeed             = s.maxSpeed;
        maxRotationSpeed     = s.maxRotationSpeed;
        rotationThrustForce  = s.rotationThrustForce;
        rotationDrag         = s.rotationDrag;
        yawDeadzoneAngle     = s.yawDeadzoneAngle;
        maxBankAngle         = s.maxBankAngle;
        bankingSpeed         = s.bankingSpeed;
        minStrafeForce       = s.minStrafeForce;
        maxStrafeForce       = s.maxStrafeForce;
    }
} 