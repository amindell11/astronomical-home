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
        public float BoostCooldownRemaining => booster.CooldownRemaining;
        private Ship parentShip;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            parentShip = GetComponent<Ship>();
            booster = new Booster();
            PlaneConstraints.ConstrainBodyToPlane(rb);
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
            ApplyForces(outs.thrust, outs.strafe, outs.boost, outs.yawTorque, outs.bank);
            ConstrainRotation();
        }


        private void ApplyForces(Vector2 thrust, Vector2 strafe, Vector2 boost, float yawTorque, float targetBank)
        {
            rb.AddForce(GamePlane.PlaneDirToWorld(thrust), ForceMode.Force);
            rb.AddForce(GamePlane.PlaneDirToWorld(strafe), ForceMode.Force);
            rb.AddForce(GamePlane.PlaneDirToWorld(boost), ForceMode.Impulse);
            rb.AddTorque(GamePlane.Normal * yawTorque, ForceMode.Force);

            // Bank: damped spring torque toward target angle around the ship's heading axis.
            var bankError = (targetBank - Kinematics.bank) * Mathf.Deg2Rad;
            var bankRate = Vector3.Dot(rb.angularVelocity, transform.up);
            rb.AddTorque(transform.up * (bankError * settings.bankTorque - bankRate * settings.bankDamping), ForceMode.Force);

            DebugForces(thrust,strafe,boost,yawTorque);
        }
        private void ConstrainRotation()
        {
            // Project transform.up onto the game plane to get the heading.
            var projectedUp = Vector3.ProjectOnPlane(transform.up, GamePlane.Normal);
            if (projectedUp.sqrMagnitude < 1e-6f)
            {
                // Degenerate case: transform.up is parallel to Normal. Force a reset.
                rb.rotation = GamePlane.Rotation;
                rb.angularVelocity = Vector3.zero;
                return;
            }

            // Rotate transform.up back onto the plane, preserving yaw and bank.
            projectedUp.Normalize();
            rb.rotation = Quaternion.FromToRotation(transform.up, projectedUp) * rb.rotation;

            // Strip pitch angular velocity so it can't accumulate.
            var pitchAxis = Vector3.Cross(projectedUp, GamePlane.Normal);
            if (pitchAxis.sqrMagnitude > 1e-6f)
            {
                pitchAxis.Normalize();
                rb.angularVelocity -= Vector3.Dot(rb.angularVelocity, pitchAxis) * pitchAxis;
            }
        }

        partial void DebugForces(Vector2 thrust, Vector2 strafe, Vector2 boost, float yaw);
    }
}
