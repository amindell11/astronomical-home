using System;
using Movement;
using UnityEngine;
using Ships.Command;
using Ships.Keystones;
using Ships.Loadout;
using Substrate;
namespace Ships.Movement
{
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(50)]
    public class MovementController : MonoBehaviour, IPilot
    {
        private Rigidbody  rb;
        private Keystone keystone;
        internal ResolvedShipStats settings;
        private PilotCommand currentCommand;
        private float speedCap;
        public Kinematics Kinematics => getKinematics();
        private Func<Kinematics> getKinematics;
        /// <summary>The piloting command applied on the most recent step (read by visuals/audio).</summary>
        public PilotCommand CurrentCommand => currentCommand;

        /// <summary>IPilot: the commander pushes the next step's piloting command here.</summary>
        public void Drive(in PilotCommand cmd) => currentCommand = cmd;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            PlaneConstraints.ConstrainBodyToPlane(rb);
        }

        private void Start()
        {
            ResetMovement();
        }

        public void Initialize(ResolvedShipStats s, Func<Kinematics> getKinematics, Keystone keystone)
        {
            this.getKinematics = getKinematics;
            this.keystone = keystone;
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
            rb.maxLinearVelocity = speedCap = s.maxSpeed;
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
            rb.maxLinearVelocity = speedCap = settings.maxSpeed;
            rb.maxAngularVelocity = settings.maxYawRate * Mathf.Deg2Rad;
            currentCommand = default;
        }

        private void FixedUpdate()
        {
            var kinematics = Kinematics;
            var effect = keystone.Step(in kinematics, currentCommand.boost, Time.fixedDeltaTime, out var activated,
                settings.maxSpeed, currentCommand.abilityTarget);
            currentCommand.boost = activated ? 1f : 0f;
            if (effect.Displacement != Vector2.zero) Jump(effect.Displacement);
            ApplySpeedCap(Time.fixedDeltaTime);
            var outs = Forces.ComputeOutputs(kinematics, currentCommand, settings, keystone.SpeedScale);
            var thrust = currentCommand.thrust > 0f ? outs.thrust * keystone.ThrustScale : outs.thrust;
            var maneuverability = keystone.ManeuverabilityScale;
            rb.maxAngularVelocity = settings.maxYawRate * maneuverability * Mathf.Deg2Rad;
            ApplyForces(thrust, outs.strafe * maneuverability, effect.Impulse, outs.yawTorque * maneuverability, outs.bank);
            ConstrainRotation();
        }


        // The respawn teleport, keeping velocity: sync the transform for same-step queries, then write the body with interpolation off so it doesn't smear the jump.
        private void Jump(Vector2 displacement)
        {
            var to = rb.position + GamePlane.PlaneDirToWorld(displacement);
            transform.position = to;
            Physics.SyncTransforms();
            var interpolation = rb.interpolation;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.position = to;
            rb.interpolation = interpolation;
        }

        // A raised cap takes effect at once; a lowered one bleeds down at the rate drag alone sheds the base cap.
        private void ApplySpeedCap(float dt)
        {
            var target = settings.maxSpeed * keystone.SpeedScale;
            speedCap = target >= speedCap ? target : Mathf.Max(target, speedCap - settings.maxSpeed * settings.linearDrag * dt);
            rb.maxLinearVelocity = speedCap;
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
