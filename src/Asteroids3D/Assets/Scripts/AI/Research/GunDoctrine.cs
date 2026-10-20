using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Movement;
using Ships;
using UnityEngine;

namespace AI.Research
{
    /// <summary>Research-only per-ship knobs for AI weapon strategy (branch research/ai-gun-strategies). Every default reproduces main's behaviour; the gun-research rig writes fields from JSON.</summary>
    public class GunDoctrine : MonoBehaviour
    {
        public string label = "base";

        [Header("Pilot")]
        public float aimAuthority = 1f;
        public float holdRange = -1f;
        public bool autoProfile;
        public float rangeLasers = 12f, rangeTurbo = 11f, rangeRippers = 9f, rangeCharge = 13f,
            rangeRail = 30f, rangeMissiles = 30f, rangeGrenades = 17f;
        public float strafeFraction;
        public float strafePeriod;
        public bool aimLock;
        public float aimAccelFrac = 0.7f;
        public float aimTau = 0.06f;
        public float aimKp = 8f;
        [System.NonSerialized] public float evadeUrgency;

        [Header("Trigger")]
        public bool smartTrigger;
        public float geomTolScale = 0f;
        public float tapCharge = 0.34f;
        public float chargeWideCone = 25f;
        public float fullChargeReleaseErr = 12f;
        public float railCommitCone = 30f;
        public float railAbortErr = -1f;
        public float railLeadCap = 0.3f;
        public float ventTo = -1f;
        public float grenadeMinRange = 14f;
        public float grenadeMaxRange = 25f;
        public float grenadeCone = 180f;

        private Ship ship;
        private bool venting;
        private bool prevHeld;

        /// <summary>The hold range this ship flies at: explicit, else its weapon's profile, else -1 (archetype shape).</summary>
        public float HoldRange
        {
            get
            {
                if (holdRange > 0f) return holdRange;
                if (!autoProfile || !ship || !ship.Weapons || !ship.Weapons.Primary) return -1f;
                var w = ship.Weapons.Primary;
                return w switch
                {
                    ChargeLasers _ => rangeCharge,
                    Railguns _ => rangeRail,
                    Missiles _ => rangeMissiles,
                    Grenades _ => rangeGrenades,
                    Rippers _ => rangeRippers,
                    Lasers l => l.Heat && l.Heat.HeatPerShot < 10f ? rangeTurbo : rangeLasers,
                    _ => -1f,
                };
            }
        }

        public static GunDoctrine Of(Component c) => c ? c.GetComponentInParent<GunDoctrine>() : null;

        private void Awake() => ship = GetComponent<Ship>();

        public void ResetState()
        {
            venting = false;
            prevHeld = false;
        }

        /// <summary>Primary-slot trigger under the smart policies; returns null to defer to main's Gunner rule.</summary>
        public bool? PrimaryHeld(bool engage, bool hasTarget, in Kinematics self, Vector2 targetPos, Vector2 targetVel,
            float targetRadius, bool baselineSolution)
        {
            if (!smartTrigger || !ship || !ship.Weapons) return null;
            var weapon = ship.Weapons.Primary;
            if (!weapon) return null;
            if (!engage || !hasTarget)
            {
                prevHeld = false;
                return false;
            }

            var held = weapon switch
            {
                ChargeLasers c => ChargeTap(c, in self, targetPos, targetVel, targetRadius),
                Railguns r => RailCommit(r, in self, targetPos, targetVel, targetRadius),
                Grenades g => GrenadeLob(g, in self, targetPos, targetVel),
                Lasers l => HeatBurst(l, in self, targetPos, targetVel, targetRadius, baselineSolution),
                _ => GeomOr(weapon, in self, targetPos, targetVel, targetRadius, baselineSolution),
            };
            prevHeld = held;
            return held;
        }

        /// <summary>Where the trigger's target point should be for weapons that place their shot (grenades); null = Gunner's aim.</summary>
        public Vector3? PrimaryTargetPoint(in Kinematics self, Vector2 targetPos, Vector2 targetVel)
        {
            if (!smartTrigger || !ship || !ship.Weapons || !(ship.Weapons.Primary is Grenades g)) return null;
            return Substrate.GamePlane.PlanePointToWorld(GrenadeAim(g, in self, targetPos, targetVel));
        }

        private float AimErr(in Kinematics self, Vector2 aim) => Vector2.Angle(self.Forward, aim - self.pos);

        private float Tolerance(WeaponComponent weapon, float dist, float targetRadius, float fallback)
        {
            if (geomTolScale <= 0f) return fallback;
            return Mathf.Max(1f, Mathf.Atan2(targetRadius * geomTolScale, Mathf.Max(dist, 0.1f)) * Mathf.Rad2Deg);
        }

        private bool GeomOr(WeaponComponent weapon, in Kinematics self, Vector2 targetPos, Vector2 targetVel,
            float targetRadius, bool baselineSolution)
        {
            if (geomTolScale <= 0f || weapon.FireRange <= 0f) return baselineSolution;
            var aim = Gunner.AimPoint(in self, targetPos, targetVel, weapon.ProjectileSpeed);
            var dist = Vector2.Distance(self.pos, targetPos);
            if (dist > weapon.FireRange || !weapon.CanFire()) return false;
            return AimErr(in self, aim) <= Tolerance(weapon, dist, targetRadius, 5f);
        }

        private bool HeatBurst(Lasers lasers, in Kinematics self, Vector2 targetPos, Vector2 targetVel,
            float targetRadius, bool baselineSolution)
        {
            var heat = lasers.Heat;
            if (ventTo >= 0f && heat)
            {
                if (heat.WouldOverheatOnNextShot()) venting = true;
                if (venting && heat.HeatPct <= ventTo) venting = false;
                if (venting) return false;
            }
            return GeomOr(lasers, in self, targetPos, targetVel, targetRadius, baselineSolution);
        }

        private bool ChargeTap(ChargeLasers weapon, in Kinematics self, Vector2 targetPos, Vector2 targetVel, float targetRadius)
        {
            var charge = weapon.Charge;
            var cooldown = weapon.GetComponent<Cooldown>();
            var dist = Vector2.Distance(self.pos, targetPos);
            var aim = Gunner.AimPoint(in self, targetPos, targetVel, weapon.ProjectileSpeed);
            var err = AimErr(in self, aim);
            var tol = Tolerance(weapon, dist, targetRadius, 5f);
            var ready = !cooldown || cooldown.CanFire();
            var inRange = dist <= weapon.FireRange;
            var pct = charge.ChargePct;

            // Charging: keep the trigger down while the shot is plausible.
            if (pct < tapCharge)
                return inRange && err <= chargeWideCone || (prevHeld && pct > 0f && inRange);
            // Charged enough: release on the first on-solution step the cooldown allows.
            if (ready && inRange && err <= tol) return false;
            // Near full: a release now beats ChargeTime's blind auto-fire at full.
            var nextPct = pct + Time.fixedDeltaTime / charge.FullChargeTime;
            if (nextPct >= 0.999f && ready && err <= fullChargeReleaseErr) return false;
            return true;
        }

        private bool RailCommit(Railguns weapon, in Kinematics self, Vector2 targetPos, Vector2 targetVel, float targetRadius)
        {
            var charge = weapon.Charge;
            var dist = Vector2.Distance(self.pos, targetPos);
            var pct = charge.ChargePct;
            var remaining = (1f - pct) * charge.FullChargeTime;
            var aimAtFire = targetPos + targetVel * remaining;
            var errAtFire = AimErr(in self, aimAtFire);
            var inRange = dist <= weapon.FireRange;
            if (pct <= 0f) return inRange && errAtFire <= railCommitCone;
            if (!inRange) return false;
            var nextPct = pct + Time.fixedDeltaTime / charge.FullChargeTime;
            if (railAbortErr > 0f && nextPct >= 0.999f)
            {
                var tol = Mathf.Max(railAbortErr, Tolerance(weapon, dist, targetRadius, 2f));
                if (AimErr(in self, targetPos) > tol) return false;
            }
            return true;
        }

        private Vector2 GrenadeAim(Grenades weapon, in Kinematics self, Vector2 targetPos, Vector2 targetVel)
        {
            var p = targetPos;
            for (var i = 0; i < 3; i++)
            {
                var d = Mathf.Min(Vector2.Distance(self.pos, p), weapon.Range);
                p = targetPos + targetVel * (d / 20f);
            }
            return p;
        }

        private bool GrenadeLob(Grenades weapon, in Kinematics self, Vector2 targetPos, Vector2 targetVel)
        {
            if (!weapon.CanFire()) return false;
            var aim = GrenadeAim(weapon, in self, targetPos, targetVel);
            var d = Vector2.Distance(self.pos, aim);
            return d >= grenadeMinRange && d <= grenadeMaxRange && AimErr(in self, aim) <= grenadeCone;
        }

        /// <summary>Where the nose should point for the primary weapon, or null to leave yaw to the MPC.</summary>
        public Vector2? AimPoint(in Kinematics self, Vector2 targetPos, Vector2 targetVel)
        {
            if (!ship || !ship.Weapons || !ship.Weapons.Primary) return null;
            var weapon = ship.Weapons.Primary;
            switch (weapon)
            {
                case Grenades _:
                    return null;
                case Railguns r:
                    var remaining = (1f - r.Charge.ChargePct) * r.Charge.FullChargeTime;
                    return targetPos + targetVel * Mathf.Min(remaining, railLeadCap);
                case Missiles _:
                    return targetPos;
                default:
                    return Gunner.AimPoint(in self, targetPos, targetVel, weapon.ProjectileSpeed);
            }
        }

        /// <summary>Replaces the MPC's yaw with a time-optimal aim controller (bearing-rate feedforward) when aim lock is on.</summary>
        public void ShapeCommand(ref Ships.Command.PilotCommand cmd, in Kinematics self, in Dynamics dyn, bool hasTarget,
            Vector2 targetPos, Vector2 targetVel)
        {
            if (!aimLock || !hasTarget || evadeUrgency > 0f) return;
            var aim = AimPoint(in self, targetPos, targetVel);
            if (!aim.HasValue) return;
            var r = aim.Value - self.pos;
            if (r.sqrMagnitude < 0.01f) return;
            var desiredYaw = Mathf.Atan2(-r.x, r.y) * Mathf.Rad2Deg;
            var err = Mathf.DeltaAngle(self.yaw, desiredYaw);
            var v = targetVel - self.vel;
            var bearingRate = (r.x * v.y - r.y * v.x) / r.sqrMagnitude * Mathf.Rad2Deg;
            var inertia = dyn.yawInertia > 0f ? dyn.yawInertia : 1f;
            var alpha = dyn.yawTorque / inertia * Mathf.Rad2Deg * aimAccelFrac;
            var approach = Mathf.Min(Mathf.Sqrt(2f * alpha * Mathf.Abs(err)), aimKp * Mathf.Abs(err));
            var maxRate = dyn.maxYawRate > 0f ? dyn.maxYawRate * Mathf.Rad2Deg : 180f;
            var targetRate = Mathf.Clamp(bearingRate + Mathf.Sign(err) * approach, -maxRate, maxRate);
            var rateErr = (targetRate - self.yawRate) * Mathf.Deg2Rad;
            var torque = inertia * (dyn.angularDrag * targetRate * Mathf.Deg2Rad + rateErr / Mathf.Max(aimTau, Time.fixedDeltaTime));
            cmd.yawTorque = Mathf.Clamp(torque / Mathf.Max(dyn.yawTorque, 1e-3f), -1f, 1f);
        }

        /// <summary>The pilot's hold-range velocity with an optional circle-strafe, or null to keep the archetype law.</summary>
        public Vector2 ShapeVelocity(Vector2 lawVelocity, in Kinematics self, in Kinematics enemy, float maxSpeed, float time)
        {
            if (strafeFraction <= 0f) return lawVelocity;
            var los = enemy.pos - self.pos;
            var r = los.magnitude;
            var losHat = r > 1e-4f ? los / r : Vector2.up;
            var sign = strafePeriod > 0f && Mathf.FloorToInt(time / strafePeriod) % 2 == 1 ? -1f : 1f;
            var tangent = sign * new Vector2(-losHat.y, losHat.x);
            return Vector2.ClampMagnitude(lawVelocity + tangent * strafeFraction * maxSpeed, maxSpeed);
        }
    }
}
