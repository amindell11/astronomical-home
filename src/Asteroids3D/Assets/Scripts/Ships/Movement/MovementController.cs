// This file contains movement and plane logic for ships.
// Damage and health are now handled by ShipHealth.

using System;
using Game;
using UnityEngine;
using Utils;

namespace Ships.Movement
{
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(50)]
    public partial class MovementController : MonoBehaviour
    {

        [Header("Debug")]
        public bool enableDebugLogs;

        [Header("Movement Gizmos")]
        public bool showMovementGizmos = true;
        public float movementGizmoScale = 3f;

        private Rigidbody  rb;
        private Booster booster;
        private Settings settings;
        private Command.Command currentCommand;
        public Kinematics Kinematics => getKinematics();
        private Func<Kinematics> getKinematics;
        internal Command.Command CurrentCommand {
            set => currentCommand = value; }
        public bool BoostAvailable => booster.BoostAvailable;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            booster = new Booster();
            AlignRotationToPlane();
        }

        private void Start()
        {
            ResetMovement();
        }

        public void Initialize(Settings s, Func<Kinematics> getKinematics)
        {
            this.settings = s;
            this.getKinematics = getKinematics;
        }
        public void PopulateSettings(Settings s)
        {
            s.onSettingsChanged.AddListener(()=>ApplySettings(s));
            ApplySettings(s);
            settings = s;
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
        }

        private void FixedUpdate()
        {
            currentCommand.boost = booster.ProcessBoost(currentCommand.boost, settings.boostCooldown);
            var outs = Forces.ComputeOutputs(Kinematics, currentCommand, settings);
            ApplyForces(outs.thrust, outs.strafe, outs.boost, outs.yawTorque);
            ApplyRotation(Kinematics.yaw, outs.bank);
            ConstrainToPlane();
        }


        private void ApplyForces(Vector2 thrust, Vector2 strafe, Vector2 boost, float yawTorque)
        {   
            rb.AddForce(GamePlane.PlaneDirToWorld(thrust)* rb.mass, ForceMode.Force);
            rb.AddForce(GamePlane.PlaneDirToWorld(strafe) * rb.mass, ForceMode.Force);
            rb.AddForce(GamePlane.PlaneDirToWorld(boost) * rb.mass, ForceMode.Impulse);
            rb.AddTorque(GamePlane.Normal * (yawTorque * rb.mass));
            DebugForces(thrust,strafe,boost,yawTorque);
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

        partial void DebugForces(Vector2 thrust, Vector2 strafe, Vector2 boost, float yaw);
    }
} 