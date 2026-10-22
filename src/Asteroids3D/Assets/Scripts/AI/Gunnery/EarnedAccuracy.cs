using AI.Context;
using Combat.Targeting;
using Movement;
using Ships.Registry;
using Substrate;
using UnityEngine;
using Utils;

namespace AI.Gunnery
{
    /// <summary>A bearing error that starts loose when the Gunner picks up a target and tightens while the target stays in the nose cone, in range and in line of sight. A new target, a jink, a boost or a lost line of sight resets it. The rules and defaults are #409's lethality calibration.</summary>
    internal sealed class EarnedAccuracy
    {
        private const float ErrorCorrelationTime = 0.5f;
        private const float AccelSmoothing = 0.1f;
        private const float MaxStep = 0.1f;
        // A boost restarts the target's cooldown, so a rise past this margin marks one.
        private const float BoostCooldownJump = 0.5f;

        private readonly AimSettings settings;
        private readonly SeedScope scope;
        private Wander error;
        private ShipId target;
        private double lastTime;
        private Vector2 lastVel, accel;
        private bool haveVel, jinkArmed, hadLineOfSight;
        private float lastBoostCooldown;

        public float Progress { get; private set; }

        public EarnedAccuracy(AimSettings settings, SeedScope scope)
        {
            this.settings = settings;
            this.scope = scope;
            Reset();
        }

        public void Reset()
        {
            error = new Wander(scope);
            target = ShipId.Invalid;
            lastTime = double.NaN;
            accel = Vector2.zero;
            haveVel = false;
            jinkArmed = true;
            hadLineOfSight = true;
            lastBoostCooldown = 0f;
            Progress = 0f;
        }

        /// <summary>This tick's bearing error in degrees, from the true target.</summary>
        public float ErrorDeg(in Kinematics self, in EnemyTarget sighted, double now)
        {
            if (settings.startingError <= 0f) return 0f;

            var dt = double.IsNaN(lastTime) ? 0f : Mathf.Min((float)(now - lastTime), MaxStep);
            lastTime = now;
            if (sighted.id != target) Acquire(in sighted);

            var los = sighted.kinematics.pos - self.pos;
            var dist = los.magnitude;
            var losHat = dist > 1e-4f ? los / dist : Vector2.up;
            ObserveJink(sighted.kinematics.vel, losHat, dt);
            ObserveBoost(in sighted);
            var lineOfSight = ObserveLineOfSight(in self, sighted.kinematics.pos);

            var inCone = Vector2.Angle(self.Forward, los) <= settings.cone;
            var inRange = settings.range <= 0f || dist <= settings.range;
            if (inCone && inRange && lineOfSight)
                Progress = Mathf.Clamp01(Progress + dt / settings.timeToTighten);

            error.Step(dt, ErrorCorrelationTime);
            return settings.startingError * (1f - Progress) * error.Value;
        }

        private void Acquire(in EnemyTarget sighted)
        {
            target = sighted.id;
            Progress = 0f;
            haveVel = false;
            jinkArmed = true;
            hadLineOfSight = true;
            lastBoostCooldown = sighted.boostCooldownRemaining;
        }

        private void ObserveJink(Vector2 vel, Vector2 losHat, float dt)
        {
            if (haveVel && dt > 0f)
                accel = Vector2.Lerp(accel, (vel - lastVel) / dt, Mathf.Clamp01(dt / Mathf.Max(AccelSmoothing, dt)));
            lastVel = vel;
            haveVel = true;

            var threshold = settings.jinkThreshold;
            if (threshold <= 0f) return;
            var lateral = Mathf.Abs(accel.x * losHat.y - accel.y * losHat.x);
            if (jinkArmed && lateral > threshold)
            {
                jinkArmed = false;
                Progress = 0f;
            }
            else if (!jinkArmed && lateral < 0.5f * threshold) jinkArmed = true;
        }

        private void ObserveBoost(in EnemyTarget sighted)
        {
            if (!settings.resetOnBoost || !sighted.id.IsValid) return;
            if (sighted.boostCooldownRemaining > lastBoostCooldown + BoostCooldownJump) Progress = 0f;
            lastBoostCooldown = sighted.boostCooldownRemaining;
        }

        private bool ObserveLineOfSight(in Kinematics self, Vector2 targetPos)
        {
            if (!settings.resetOnLostLineOfSight) return true;
            var clear = TargetingMath.HasLineOfSight(in self, GamePlane.PlanePointToWorld(targetPos));
            if (!clear && hadLineOfSight) Progress = 0f;
            hadLineOfSight = clear;
            return clear;
        }
    }
}
