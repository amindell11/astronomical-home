// This file contains movement and plane logic for ships.
// Damage and health are now handled by ShipHealth.

using Game;
using UnityEngine;
using Utils;

namespace ShipMain.Movement
{
    [RequireComponent(typeof(Rigidbody))]
    public class Controller : MonoBehaviour
    {

        [Header("Debug")]
        public bool enableDebugLogs;

        [Header("Movement Gizmos")]
        public bool showMovementGizmos = true;
        public float movementGizmoScale = 3f;

        private Ship ship;
        private Rigidbody  rb;
        private Settings settings;
        internal Command CurrentCommand;
        public Kinematics Kinematics { get; private set; }

        private Mover phys;
        public float Mass => rb.mass;
        public bool BoostAvailable => phys.BoostAvailable;

        private void Awake()
        {
            ship = GetComponent<Ship>();
            rb = GetComponent<Rigidbody>();
            rb.useGravity  = false;
            phys = new Mover();
        }

        private void Start()
        {
            AlignRotationToPlane();
            ResetMovement();
            GetStateFrom3D();
            if (!settings) return;
            ApplySettings();
        }

        private void ApplySettings()
        {
            if (!rb) return;
            rb.maxLinearVelocity = settings.maxSpeed;
            rb.maxAngularVelocity = settings.maxRotationSpeed;
            rb.linearDamping = settings.linearDrag;
            rb.angularDamping = settings.rotationDrag;
            rb.mass = settings.mass;
        }
   

        public void ResetMovement()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Kinematics = new Kinematics(Vector2.zero, Vector2.zero, 0, 0, 0);
        }

        private void FixedUpdate()
        {
            Kinematics = GetStateFrom3D();

            var (thrust, strafe, boost, yawTorque, bank) = phys.CalculateInputs(Kinematics, CurrentCommand, settings);
            
            ApplyForces(thrust, strafe, boost, yawTorque);
            ApplyRotation(Kinematics.Yaw, bank);
            
            ConstrainToPlane();
        }
    
        private Kinematics GetStateFrom3D()
        {
            var pos = GamePlane.WorldPointToPlane(transform.position);
            var vel = GamePlane.WorldPointToPlane(rb.linearVelocity);
            var yaw = Vector3.SignedAngle(GamePlane.Forward, transform.up, GamePlane.Normal);
            float yawRate = Vector3.Dot(rb.angularVelocity, GamePlane.Normal) * Mathf.Rad2Deg;
            float bank = Vector3.SignedAngle(GamePlane.Normal, transform.forward, transform.up);
            return new Kinematics(pos, vel, yaw, yawRate, bank);
        }   

        private void ApplyForces(Vector2 thrust, Vector2 strafe, Vector2 boost, float yawTorque)
        {   
            rb.AddForce(GamePlane.PlaneDirToWorld(thrust), ForceMode.Force);
            rb.AddForce(GamePlane.PlaneDirToWorld(strafe), ForceMode.Force);
            rb.AddForce(GamePlane.PlaneDirToWorld(boost), ForceMode.Impulse);
            rb.AddTorque(GamePlane.Normal * yawTorque);
        }
        private void ApplyRotation(float yaw, float bank)
        {
            var qYaw = Quaternion.AngleAxis(yaw, Vector3.forward);
            var qBank = Quaternion.AngleAxis(bank, Vector3.up);
            transform.rotation = (GamePlane.Plane.rotation) * qYaw * qBank;
        }

        private void ConstrainToPlane()
        {
            var n = GamePlane.Normal;
            var d   = Vector3.Dot(transform.position - GamePlane.Origin, n);
            transform.position -= n * d;
            rb.linearVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, n);
        }
        private void AlignRotationToPlane()
        {
            var projectedUp = Vector3.ProjectOnPlane(transform.up, GamePlane.Normal).normalized;
            if (projectedUp.sqrMagnitude < 1e-6f) return;
            var toPlane = Quaternion.FromToRotation(transform.up, projectedUp);
            transform.rotation = toPlane * transform.rotation;
        }

        public void PopulateSettings(Settings s)
        {
            settings = s;
            ApplySettings();
        }
    
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showMovementGizmos || !ship) return;

            var pos = transform.position;
            float scale = movementGizmoScale;

            SuperGizmos.DrawArrow(pos, transform.up * CurrentCommand.Thrust, 
                SuperGizmos.HeadType.Sphere, 0.15f, Color.yellow, scale);

            SuperGizmos.DrawArrow(pos, transform.right * CurrentCommand.Strafe, 
                SuperGizmos.HeadType.Cube, 0.25f, Color.yellow, scale);
        }
#endif
    }
} 