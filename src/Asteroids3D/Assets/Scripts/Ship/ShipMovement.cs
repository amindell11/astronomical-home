// This file contains movement and plane logic for ships.
// Damage and health are now handled by ShipHealth.

using UnityEngine;
using System.Collections;
using UnityEngine.Serialization;
using ShipControl;

[RequireComponent(typeof(Rigidbody))]
public class ShipMovement : MonoBehaviour
{
    private Ship ship;
    public ShipSettings settings { get; private set; }
    public ShipCommand CurrentCommand { get; private set; }
    // -------- Nested 2-D movement model --------
    public class ShipMovement2D
    {
        private ShipSettings settings;

        // 2-D state
        public ShipKinematics Kinematics { get; internal set; }
        public Vector2 ForceVector       { get; private set; }
        
        public ShipMovement2D(ShipSettings settings)
        {
            this.settings = settings;
        }

        public void UpdateSettings(ShipSettings newSettings)
        {
            this.settings = newSettings;
        }

        public void Update(float dt, ShipCommand command)
        {
            // The kinematics struct is immutable. To update it, we create a new one.
            var currentKinematics = Kinematics;
            var (newAngle, newYawRate) = ApplyYawRotation(dt, currentKinematics, command);
            var (newPosition, newVelocity) = ApplyThrust(dt, currentKinematics, command);

            Kinematics = new ShipKinematics(newPosition, newVelocity, newAngle, newYawRate);
        }

        (float, float) ApplyYawRotation(float dt, ShipKinematics kin, ShipCommand command)
        {
            float angularVelocity = kin.YawRate;
            if (command.YawRate != 0.0f)
            {
                angularVelocity += command.YawRate * settings.rotationThrustForce * dt;
            }
            else if (command.RotateToTarget)
            {
                float diff = Mathf.DeltaAngle(kin.AngleDeg, command.TargetAngle);
                if (Mathf.Abs(diff) > settings.yawDeadzoneAngle)
                {
                    float ratio   = Mathf.Abs(diff) / 180f;
                    float mult    = Mathf.Pow(ratio + 0.01f, 1f / 6f);
                    angularVelocity += Mathf.Sign(diff) * settings.rotationThrustForce * mult * dt;
                }
            }
            angularVelocity *= settings.rotationDrag;
            angularVelocity  = Mathf.Clamp(angularVelocity, -settings.maxRotationSpeed, settings.maxRotationSpeed);

            float angle = kin.AngleDeg + angularVelocity * dt;
            if (angle > 360) angle -= 360;
            if (angle < 0)   angle += 360;
            
            return (angle, angularVelocity);
        }

        (Vector2, Vector2) ApplyThrust(float dt, ShipKinematics kin, ShipCommand command)
        {
            float   angRad   = kin.AngleDeg * Mathf.Deg2Rad;
            Vector2 forward  = new Vector2(-Mathf.Sin(angRad), Mathf.Cos(angRad));
            float thrustForce = command.Thrust >= 0 ? settings.forwardAcceleration : settings.reverseAcceleration;
            Vector2 thrustV  = forward * command.Thrust * thrustForce;

            float speedRatio = kin.Vel.magnitude / settings.maxSpeed;
            float strafeF    = Mathf.Lerp(settings.maxStrafeForce, settings.minStrafeForce, speedRatio);
            Vector2 right    = new Vector2(forward.y, -forward.x);
            Vector2 strafeV  = right * command.Strafe * strafeF;

            ForceVector = thrustV + strafeV;
            
            // The Rigidbody handles the actual integration. We just provide the force.
            // The new state will be synced from the Rigidbody on the next frame.
            return (kin.Pos, kin.Vel);
        }
    }
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
    public float Mass => rb.mass;
    public float BoostAvailable => Mathf.Max(0f, nextBoostTime - Time.time);

    // --- Boost cooldown tracking ---
    private float nextBoostTime = 0f;

    // Latest kinematics snapshot
    public ShipKinematics Kinematics => Controller.Kinematics;

    // Damage flash has moved to ShipHealth

    // -------------------------------------------------
    void Awake()
    {
        ship = GetComponent<Ship>();
        rb = GetComponent<Rigidbody>();
        rb.linearDamping        = 0.2f;
        rb.angularDamping = 0f;
        rb.useGravity  = false;

        // Initialize Controller with default settings to avoid null reference issues
        Controller = new ShipMovement2D(ScriptableObject.CreateInstance<ShipSettings>());
        referencePlane = GamePlane.Plane;
        SyncAngleFrom3D();

        nextBoostTime = 0f;
    }

    void Start()
    {
        if (settings != null)
            rb.maxLinearVelocity = settings.maxSpeed;
    }

    void FixedUpdate()
    {
        if (Controller == null) return;
        
        SyncStateFrom3D();
        Controller.Update(Time.fixedDeltaTime, CurrentCommand);
        ApplyForces();
        ApplyRotation();
        ConstrainToPlane();
    }

    // ----- Movement helpers (Sync, Apply, etc.) --------------------------
    void SyncStateFrom3D()
    {
        var currentKinematics = Controller.Kinematics;
        var pos = GamePlane.WorldToPlane(transform.position);
        var vel = GamePlane.WorldToPlane(rb.linearVelocity);
        Controller.Kinematics = new ShipKinematics(pos, vel, currentKinematics.AngleDeg, currentKinematics.YawRate);
    }
    void SyncAngleFrom3D()
    {
        Vector3 planeNormal      = GamePlane.Normal;
        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.up, planeNormal).normalized;
        if (projectedForward.sqrMagnitude > 0.01f)
        {
            float ang = Vector3.SignedAngle(GamePlane.Forward, projectedForward, planeNormal);
            Controller.Kinematics = new ShipKinematics(Vector2.zero, Vector2.zero, ang < 0 ? ang + 360f : ang, 0);
        }
    }
    void ApplyForces()
    {
        // Apply continuous thrust/strafe forces from the 2-D controller.
        rb.AddForce(GamePlane.PlaneVectorToWorld(Controller.ForceVector));

        // Apply a one-shot boost impulse if requested by the current command.
        if (CurrentCommand.Boost > 0f && settings != null && Time.time >= nextBoostTime)
        {
            // Forward2D is already normalized in plane space; convert to world and scale by boost impulse.
            var boostDir = GamePlane.PlaneVectorToWorld(Forward2D).normalized;
            rb.AddForce(boostDir * settings.boostImpulse * Mathf.Clamp01(CurrentCommand.Boost), ForceMode.Impulse);

            // Set next allowed boost time
            nextBoostTime = Time.time + settings.boostCooldown;
        }
    }
    void ApplyRotation()
    {
        q_yaw  = Quaternion.AngleAxis(Controller.Kinematics.AngleDeg, Vector3.forward);
        float targetBank = -CurrentCommand.Strafe * settings.maxBankAngle;
        Quaternion q_targetBank = Quaternion.AngleAxis(targetBank, Vector3.up);
        q_bank = Quaternion.Lerp(q_bank, q_targetBank, settings.bankingSpeed * Time.fixedDeltaTime);
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
    public void ResetMovement()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        if (Controller != null)
        {
            Controller.Kinematics = new ShipKinematics(Controller.Kinematics.Pos, Vector2.zero, Controller.Kinematics.AngleDeg, 0);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showMovementGizmos || Controller == null || ship == null) return;

        Vector3 pos   = transform.position;
        float   scale = movementGizmoScale;

        // Single color for all raw movement gizmos
        Gizmos.color = Color.yellow;

        // Thrust vector (sphere head)
        Vector3 thrustVec = transform.up * CurrentCommand.Thrust * scale;
        Gizmos.DrawLine(pos, pos + thrustVec);
        Gizmos.DrawSphere(pos + thrustVec, 0.15f);

        // Strafe vector (cube head)
        Vector3 strafeVec = transform.right * CurrentCommand.Strafe * scale;
        Gizmos.DrawLine(pos, pos + strafeVec);
        Gizmos.DrawCube(pos + strafeVec, Vector3.one * 0.25f);
    }
#endif

    // ---------------- 2-D Kinematics helpers (guidance pipeline) ----------------
    public Vector2 Position2D => Controller != null ? Controller.Kinematics.Pos : Vector2.zero;
    public Vector2 Velocity2D => Controller != null ? Controller.Kinematics.Vel : Vector2.zero;
    public Vector2 Forward2D
    {
        get
        {
            float a = Controller != null ? Controller.Kinematics.AngleDeg * Mathf.Deg2Rad : 0f;
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
        this.settings = s;
        Controller?.UpdateSettings(s);
    }

    public void SetCommand(ShipCommand command)
    {
        CurrentCommand = command;
    }
} 