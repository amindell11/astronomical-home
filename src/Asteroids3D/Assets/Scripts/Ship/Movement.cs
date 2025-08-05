// This file contains movement and plane logic for ships.
// Damage and health are now handled by ShipHealth.

using UnityEngine;
using ShipControl;

[RequireComponent(typeof(Rigidbody))]
public class Movement : MonoBehaviour
{

    [Header("Debug")]
    public bool enableDebugLogs;

    [Header("Movement Gizmos")]
    public bool showMovementGizmos = true;
    public float movementGizmoScale = 3f;

    private Ship ship;
    private Settings settings;
    private Command currentCommand;
    private Rigidbody  rb;
    private Quaternion qBank = Quaternion.identity;
    private float nextBoostTime = 0f;

    public float Mass => rb.mass;
    public Kinematics Kinematics { get; private set; }
    public bool BoostAvailable => Time.time > nextBoostTime;
    
    private void Awake()
    {
        ship = GetComponent<Ship>();
        rb = GetComponent<Rigidbody>();
        rb.useGravity  = false;
    }

    private void Start()
    {
        ResetMovement();
        SyncStateFrom3D();
        if (!settings) return;
        ApplySettings();
    }

    private void ApplySettings()
    {
        rb.maxLinearVelocity = settings.maxSpeed;
        rb.maxAngularVelocity = settings.maxRotationSpeed;
        rb.linearDamping = settings.linearDrag;
        rb.angularDamping = settings.rotationDrag;
        rb.mass = settings.mass;
    }
    public void ResetMovement()
    {
        nextBoostTime = 0f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0, 0);
    }

    private void FixedUpdate()
    {
        SyncStateFrom3D();
        ApplyForces();
        ApplyRotation();
        ConstrainToPlane();
    }
    
    private void SyncStateFrom3D()
    {
        var pos = GamePlane.WorldToPlane(transform.position);
        var vel = GamePlane.WorldToPlane(rb.linearVelocity);
        var yaw = Vector3.SignedAngle(GamePlane.Forward, transform.up, GamePlane.Normal);
        float yawRate = Vector3.Dot(rb.angularVelocity, GamePlane.Normal) * Mathf.Rad2Deg;
        Kinematics = new Kinematics(pos, vel, yaw, yawRate);
    }   

    private void ApplyForces()
    {
        rb.AddForce(GamePlane.PlaneVectorToWorld(CalculateThrust(Kinematics, currentCommand)));
        rb.AddForce(GamePlane.PlaneVectorToWorld(CalculateBoost(Kinematics, currentCommand)), ForceMode.Impulse);
        rb.AddForce(GamePlane.PlaneVectorToWorld(CalculateStrafe(Kinematics, currentCommand)));
        rb.AddTorque(GamePlane.Normal * CalculateYawTorque(Kinematics, currentCommand));
    }
    private void ApplyRotation()
    {
        var qYaw = Quaternion.AngleAxis(Kinematics.Yaw, Vector3.forward);
        qBank = CalculateBank(Kinematics, currentCommand);
        transform.rotation = (GamePlane.Plane.rotation) * qYaw * qBank;
    }
    
    private void ConstrainToPlane()
    {
        var n = GamePlane.Normal;
        var d   = Vector3.Dot(transform.position - GamePlane.Origin, n);
        transform.position -= n * d;
        rb.linearVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, n);
    }
    
    private Vector2 CalculateBoost(Kinematics kin, Command command)
    {
        if (!(command.Boost > 0f) || !settings || !BoostAvailable) return Vector2.zero;
        var boostForce = kin.Forward * (settings.boostImpulse * Mathf.Clamp01(command.Boost));
        nextBoostTime = Time.time + settings.boostCooldown;
        return boostForce;
    }

    private Vector2 CalculateThrust(Kinematics kin, Command command)
    {
        var mag = command.Thrust >= 0 ? settings.forwardAcceleration : settings.reverseAcceleration;
        var thrustV = kin.Forward * (command.Thrust * mag);
        return thrustV;
    }

    private Vector2 CalculateStrafe(Kinematics kin, Command command)
    {
        var speedPct = kin.Vel.magnitude / settings.maxSpeed;
        var mag = Mathf.Lerp(settings.maxStrafeForce, settings.minStrafeForce, speedPct);
        var right = new Vector2(kin.Forward.y, -kin.Forward.x);
        var strafeV = right * (command.Strafe * mag);
        return strafeV;
    }

    private float CalculateYawTorque(Kinematics kin, Command command)
    {
        float scale = 0;
        if (command.YawTorque != 0.0f)
        {
            scale = command.YawTorque;
        }
        else if (command.RotateToTarget)
        {
            scale = RotationalThrustToTarget(command.TargetAngle, kin.Yaw, settings.yawDeadzoneAngle);
        }
        
        return scale * settings.rotationThrustForce;
    }
    
    private Quaternion CalculateBank(Kinematics kin, Command command)
    {
        float targetBank = -command.Strafe * settings.maxBankAngle;
        var qTargetBank = Quaternion.AngleAxis(targetBank, Vector3.up);
        qBank = Quaternion.Lerp(qBank, qTargetBank, settings.bankingSpeed * Time.fixedDeltaTime); 
        return qBank;
    }

    private static float RotationalThrustToTarget(float targetAngle, float yaw, float deadZone)
    {
        float diff = Mathf.DeltaAngle(yaw, targetAngle);
        if (!(Mathf.Abs(diff) > deadZone))  return 0;
        
        float ratio   = Mathf.Abs(diff) / 180f;
        float mult    = Mathf.Pow(ratio + 0.01f, 1f / 6f);
        return Mathf.Sign(diff) * mult;
    }
    
    public void PopulateSettings(Settings s)
    {
        settings = s;
        ApplySettings();
    }

    public void SetCommand(Command command)
    {
        currentCommand = command;
    }
    
    #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showMovementGizmos || !ship) return;

            var pos   = transform.position;
            float   scale = movementGizmoScale;

            // Single color for all raw movement gizmos
            Gizmos.color = Color.yellow;

            // Thrust vector (sphere head)
            var thrustVec = transform.up * currentCommand.Thrust * scale;
            Gizmos.DrawLine(pos, pos + thrustVec);
            Gizmos.DrawSphere(pos + thrustVec, 0.15f);

            // Strafe vector (cube head)
            var strafeVec = transform.right * currentCommand.Strafe * scale;
            Gizmos.DrawLine(pos, pos + strafeVec);
            Gizmos.DrawCube(pos + strafeVec, Vector3.one * 0.25f);
        }
    #endif
} 