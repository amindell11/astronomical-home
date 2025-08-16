// This file contains movement and plane logic for ships.
// Damage and health are now handled by ShipHealth.

using Game;
using ShipMain;
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

        private Rigidbody  rb;
        private Actuator actuator;
        internal Command CurrentCommand { get => actuator.CurrentCommand; set => actuator.SetCommand(value); }
        public Kinematics Kinematics => actuator.Kinematics;
        public bool BoostAvailable => actuator.BoostAvailable;
        public float Mass => rb.mass;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            actuator = new Actuator();
        }

        private void Start()
        {
            AlignRotationToPlane();
            ResetMovement();
            GetStateFrom3D();
        }

        public void PopulateSettings(Settings s)
        {
            ApplySettings(s);
            actuator?.SetSettings(s);
        }
        
        private void ApplySettings(Settings settings)
        {
            if (!rb) return;
            rb.maxLinearVelocity = settings.maxSpeed;
            rb.maxAngularVelocity = settings.maxRotationSpeed;
            rb.linearDamping = settings.linearDrag;
            rb.angularDamping = settings.rotationDrag;
            rb.mass = settings.mass;
            rb.useGravity  = false;
        }

        public void ResetMovement()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            actuator.SetKinematics(new Kinematics(Vector2.zero, Vector2.zero, 0, 0, 0));
        }

        private void FixedUpdate()
        {
            var state = GetStateFrom3D();
            var outs = actuator.GetOutputs(state);
            ApplyForces(outs.Thrust, outs.Strafe, outs.Boost, outs.YawTorque);
            ApplyRotation(state.Yaw, outs.Bank);
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

    
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showMovementGizmos) return;

            var pos = transform.position;
            float scale = movementGizmoScale;

            SuperGizmos.DrawArrow(pos, GamePlane.PlaneDirToWorld(actuator.Outputs.Thrust), 
                SuperGizmos.HeadType.Sphere, 0.15f, Color.yellow, scale);

            SuperGizmos.DrawArrow(pos, GamePlane.PlaneDirToWorld(actuator.Outputs.Strafe), 
                SuperGizmos.HeadType.Cube, 0.25f, Color.yellow, scale);
        }
#endif
    }
} 