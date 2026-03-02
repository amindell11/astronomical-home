// This file contains movement and plane logic for ships.
// Damage and health are now handled by ShipHealth.

using System;
using Game;
using Movement;
using UnityEngine;

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
        private ShipSettings settings;
        private Command.Command currentCommand;
        public Kinematics Kinematics => getKinematics();
        private Func<Kinematics> getKinematics;
        internal Command.Command CurrentCommand {
            set => currentCommand = value; }
        public bool BoostAvailable => booster.BoostAvailable;
        private Ship parentShip;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            parentShip = GetComponent<Ship>();
            booster = new Booster();
            PlaneConstraints.AlignTransformUpToPlane(transform);
        }

        private void Start()
        {
            ResetMovement();
        }

        public void Initialize(ShipSettings s, Func<Kinematics> getKinematics)
        {
            this.getKinematics = getKinematics;
            PopulateSettings(s);
        }
        
        public void PopulateSettings(ShipSettings s)
        {
            if (!s) return;
            if (settings && settings != s) settings.onSettingsChanged.RemoveAllListeners();
            s.onSettingsChanged.AddListener(()=>ApplySettings(s));
            ApplySettings(s);
            settings = s;
        }
        
        private void ApplySettings(ShipSettings s)
        {
            if (!rb) return;
            rb.maxLinearVelocity = s.maxSpeed;
            // Rigidbody.maxAngularVelocity is in radians/second, but Settings.maxYawRate is in degrees/second.
            rb.maxAngularVelocity = s.maxYawRate * Mathf.Deg2Rad;
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
            currentCommand = parentShip.CurrentCommand;

            currentCommand.boost = booster.ProcessBoost(currentCommand.boost, settings.boostCooldown);
            var outs = Forces.ComputeOutputs(Kinematics, currentCommand, settings);
            ApplyForces(outs.thrust, outs.strafe, outs.boost, outs.yawTorque);
            ApplyRotation(Kinematics.yaw, outs.bank);
            ConstrainToPlane();
        }


        private void ApplyForces(Vector2 thrust, Vector2 strafe, Vector2 boost, float yawTorque)
        {   
            rb.AddForce(GamePlane.PlaneDirToWorld(thrust), ForceMode.Force);
            rb.AddForce(GamePlane.PlaneDirToWorld(strafe), ForceMode.Force);
            rb.AddForce(GamePlane.PlaneDirToWorld(boost), ForceMode.Impulse);
            rb.AddTorque(GamePlane.Normal * yawTorque, ForceMode.Force);
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
            PlaneConstraints.ConstrainPositionAndVelocity(transform, rb);
        }

        partial void DebugForces(Vector2 thrust, Vector2 strafe, Vector2 boost, float yaw);
    }
} 
