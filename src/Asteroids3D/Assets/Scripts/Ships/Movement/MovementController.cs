// This file contains movement and plane logic for ships.
// Damage and health are now handled by ShipHealth.

using Game;
using UnityEngine;
using Utils;

namespace Ships.Movement
{
    [RequireComponent(typeof(Rigidbody))]
    public partial class MovementController : MonoBehaviour
    {

        [Header("Debug")]
        public bool enableDebugLogs;

        [Header("Movement Gizmos")]
        public bool showMovementGizmos = true;
        public float movementGizmoScale = 3f;

        private Rigidbody  rb;
        private FlightComputer flightComputer;
        internal Command CurrentCommand { get => flightComputer.CurrentCommand; set => flightComputer.SetCommand(value); }
        public Kinematics Kinematics => flightComputer.Kinematics;
        public bool BoostAvailable => flightComputer.BoostAvailable;
        public float Mass => rb.mass;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            flightComputer = new FlightComputer();
            AlignRotationToPlane();
        }

        private void Start()
        {
            ResetMovement();
            GetStateFrom3D();
        }

        public void PopulateSettings(Settings s)
        {
            s.onSettingsChanged.AddListener(()=>ApplySettings(s));
            ApplySettings(s);
            flightComputer?.PopulateSettings(s);
        }
        
        private void ApplySettings(Settings s)
        {
            if (!rb) return;
            rb.maxLinearVelocity = s.maxSpeed;
            rb.maxAngularVelocity = s.maxYawRate;
            rb.linearDamping = s.linearDrag;
            rb.angularDamping = s.angularDrag;
            rb.mass = s.mass;
        }

        public void ResetMovement()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            flightComputer.SetKinematics(new Kinematics(Vector2.zero, Vector2.zero, 0, 0, 0));
        }

        private void FixedUpdate()
        {
            var state = GetStateFrom3D();
            var outs = flightComputer.Process(state);
            ApplyForces(outs.Thrust, outs.Strafe, outs.Boost, outs.YawTorque);
            ApplyRotation(state.Yaw, outs.Bank);
            ConstrainToPlane();
        }
    
        private Kinematics GetStateFrom3D()
        {
            var pos = GamePlane.WorldPointToPlane(transform.position);
            var vel = GamePlane.WorldPointToPlane(rb.linearVelocity);
            var yaw = Vector3.SignedAngle(GamePlane.Forward, transform.up, GamePlane.Normal);
            var yawRate = Vector3.Dot(rb.angularVelocity, GamePlane.Normal) * Mathf.Rad2Deg;
            var bank = Vector3.SignedAngle(GamePlane.Normal, transform.forward, transform.up);
            return new Kinematics(pos, vel, yaw, yawRate, bank);
        }   

        private void ApplyForces(Vector2 thrust, Vector2 strafe, Vector2 boost, float yawTorque)
        {   
            rb.AddForce(GamePlane.PlaneDirToWorld(thrust)* rb.mass, ForceMode.Force);
            rb.AddForce(GamePlane.PlaneDirToWorld(strafe) * rb.mass, ForceMode.Force);
            rb.AddForce(GamePlane.PlaneDirToWorld(boost) * rb.mass, ForceMode.Impulse);
            rb.AddTorque(GamePlane.Normal * (yawTorque * rb.mass));
        }
        private void ApplyRotation(float yaw, float bank)
        {
            var qYaw = Quaternion.AngleAxis(yaw, Vector3.forward);
            var qBank = Quaternion.AngleAxis(bank, Vector3.up);
            rb.MoveRotation((GamePlane.Plane.rotation) * qYaw * qBank);
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
    }
} 