using System;
using Game;
using Movement;
using UnityEngine;
using Ships.Command;
namespace Ships.Movement
{
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(50)]
    public class MovementController : MonoBehaviour, IPilot
    {

        [Header("Debug")]
        public bool enableDebugLogs;

        [Header("Movement Gizmos")]
        public bool showMovementGizmos = true;
        public float movementGizmoScale = 3f;

        private Rigidbody  rb;
        private Booster booster;
        internal ResolvedShipStats settings;
        private PilotCommand currentCommand;
        public Kinematics Kinematics => getKinematics();
        private Func<Kinematics> getKinematics;
        /// <summary>The piloting command applied on the most recent step (read by visuals/audio).</summary>
        public PilotCommand CurrentCommand => currentCommand;
        public bool BoostAvailable => booster.BoostAvailable;
        public float BoostCooldownRemaining => booster.CooldownRemaining;
        public float BoostCooldownPct => settings != null && settings.boostCooldown > 0f
            ? booster.CooldownRemaining / settings.boostCooldown : 0f;

        /// <summary>IPilot: the commander pushes the next step's piloting command here.</summary>
        public void Drive(in PilotCommand cmd) => currentCommand = cmd;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            booster = new Booster();
            PlaneConstraints.ConstrainBodyToPlane(rb);
        }

        private void Start()
        {
            ResetMovement();
        }

        public void Initialize(ResolvedShipStats s, Func<Kinematics> getKinematics)
        {
            this.getKinematics = getKinematics;
            PopulateSettings(s);
        }

        public void PopulateSettings(ResolvedShipStats s)
        {
            if (s == null) return;
            ApplySettings(s);
            settings = s;
        }

        private void ApplySettings(ResolvedShipStats s)
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
            booster.Reset();
            currentCommand = default;
        }

        private void FixedUpdate()
        {
            booster.Tick(Time.fixedDeltaTime);
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

            var bankError = (targetBank - Kinematics.bank) * Mathf.Deg2Rad;
            var bankRate = Vector3.Dot(rb.angularVelocity, transform.up);
            rb.AddTorque(transform.up * (bankError * settings.bankTorque - bankRate * settings.bankDamping), ForceMode.Force);

#if UNITY_EDITOR
            DebugForces(thrust, strafe, boost, yawTorque);
#endif
        }
        private void ConstrainRotation()
        {
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

#if UNITY_EDITOR
        internal Vector2 dbgThrust, dbgStrafe, dbgBoost;
        internal float dbgYaw;

        private void DebugForces(Vector2 thrust, Vector2 strafe, Vector2 boost, float yaw)
        {
            dbgThrust = thrust;
            dbgStrafe = strafe;
            dbgBoost = boost;
            dbgYaw = yaw;
        }
#endif
    }
}
