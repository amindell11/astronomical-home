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
        public bool matchupRange;
        public float outrangeMargin = 6f;
        public bool extendGunRange;
        public float gunRangeFrac = 0.7f;
        [System.NonSerialized] public Ship enemy;
        public float strafeFraction;
        public float strafePeriod;
        public bool hardJink;
        public bool aimLock;
        public float aimAccelFrac = 0.7f;
        public float aimTau = 0.06f;
        public float aimKp = 8f;
        [System.NonSerialized] public float evadeUrgency;

        [Header("Trigger")]
        public bool smartTrigger;
        public float geomTolScale = 0f;
        public float tapCharge = 0.34f;
        public float chargeTolScale = -1f;
        public float chargeWideCone = 25f;
        public float fullChargeReleaseErr = 12f;
        public float railCommitCone = 30f;
        public float railAbortErr = -1f;
        public float railLeadCap = 0.3f;
        public float railTolScale = -1f;
        public float railMaxTargetBank = -1f;
        public float railSteady;
        [System.NonSerialized] public float targetBank;
        public bool aimAccelFF;
        public float accelSmoothing = 0.3f;
        private Vector2 lastTargetVel;
        private Vector2 targetAccel;
        private bool haveLastVel;
        public float ventTo = -1f;
        public float grenadeMinRange = 14f;
        public float grenadeMaxRange = 25f;
        public float grenadeCone = 180f;

        [Header("Research tail (2026-10-20)")]
        public float mpcFacingWidth = -1f;
        public float mpcYawRate = -1f;
        public float mpcNoiseStd = -1f;
        public int mpcSamples = -1;
        public float grenadeSafeMargin = -1f;
        public float grenadeChainGap = -1f;
        public bool grenadeBackoff;
        public float grenadeBackoffExtra = 2f;
        public bool railOrbit;
        public float railOrbitRate = 90f;
        public float railOrbitCommit = 12f;
        public float skill = -1f;
        public float skillReactionMax = 0.3f;
        public float skillNoiseMax = 10f;
        public float skillNoiseTau = 0.5f;
        public float skillAccelMin = 0.1f;
        public float skillCurve = 1f;
        public float skillTolGain;
        public int skillSeed = 1;

        [Header("Earned accuracy (2026-10-21)")]
        public float eaE0 = -1f;
        public float eaT = 4f;
        public float eaCurve = 1f;
        public float eaCone = 30f;
        public float eaRange = -1f;
        public float eaLoseRate;
        public float eaJinkAccel = -1f;
        public float eaJinkReset = 1f;
        public float eaBoostReset = -1f;
        public float eaLosReset = 1f;
        public float eaReacquireReset = 1f;
        public float eaNoiseTau = 0.5f;
        public float eaAccelSmooth = 0.1f;
        public int eaSeed = 1;
        [System.NonSerialized] public float eaProgress;
        [System.NonSerialized] public float eaAngle;
        [System.NonSerialized] public float eaLatAccel;
        [System.NonSerialized] public int eaJinks, eaBoosts, eaLosBreaks, eaReacquires;
        private Ship eaTarget;
        private System.Random eaRng;
        private float eaW;
        private double eaLastTime = double.NaN;
        private Vector2 eaLastVel, eaAccel;
        private bool eaHaveVel;
        private bool eaJinkArmed = true;
        private bool eaHadLos = true;
        private float eaLastBoostCd;
        private double nextGrenadeOk;
        private float lastGrenadeAimDist;
        private bool grenadeHooked;

        private Ship ship;
        private bool venting;
        private bool prevHeld;
        private readonly System.Collections.Generic.List<(double t, Vector2 pos, Vector2 vel)> perceived = new();
        private System.Random noiseRng;
        private float noiseAngle;
        private double lastNoiseTime = double.NaN;

        /// <summary>The hold range this ship flies at: explicit, else its weapon's profile, else -1 (archetype shape).</summary>
        public float HoldRange
        {
            get
            {
                var preferred = PreferredRange;
                if (!matchupRange || preferred <= 0f || !enemy || !ship || !ship.Weapons || !ship.Weapons.Primary)
                    return preferred;
                var myReach = EffectiveFireRange(ship.Weapons.Primary);
                var enemyDoctrine = enemy.GetComponent<GunDoctrine>();
                var enemyWeapon = enemy.Weapons ? enemy.Weapons.Primary : null;
                if (!enemyWeapon) return preferred;
                var enemyReach = enemyDoctrine ? enemyDoctrine.EffectiveFireRange(enemyWeapon) : EffectiveFireRange(enemyWeapon);
                var standoff = enemyReach + outrangeMargin;
                if (myReach - 2f >= standoff) return Mathf.Max(preferred, standoff);
                return preferred;
            }
        }

        /// <summary>The farthest range at which this doctrine lets the weapon fire.</summary>
        public float EffectiveFireRange(WeaponComponent w)
        {
            switch (w)
            {
                case Railguns r: return r.FireRange;
                case Missiles _: return 60f;
                case Grenades g: return Mathf.Min(grenadeMaxRange, g.Range);
                case WeaponBase<Combat.Projectiles.Laser> gun:
                    return extendGunRange && gun.projectilePrefab
                        ? Mathf.Max(w.FireRange, gun.projectilePrefab.MaxDistance * gunRangeFrac)
                        : w.FireRange;
                default: return w.FireRange;
            }
        }

        private float PreferredRange
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
            haveLastVel = false;
            targetAccel = Vector2.zero;
            perceived.Clear();
            noiseRng = new System.Random(skillSeed);
            noiseAngle = 0f;
            lastNoiseTime = double.NaN;
            nextGrenadeOk = 0;
            eaProgress = 0f;
            eaAngle = 0f;
            eaW = 0f;
            eaRng = new System.Random(eaSeed);
            eaLastTime = double.NaN;
            eaTarget = null;
            eaHaveVel = false;
            eaAccel = Vector2.zero;
            eaJinkArmed = true;
            eaHadLos = true;
            eaJinks = eaBoosts = eaLosBreaks = eaReacquires = 0;
        }

        /// <summary>Earned accuracy: the Gunner's anchor ship this tick. A new ship is an acquisition; a switch away from a live one is a re-acquisition.</summary>
        public void NoteTarget(Ship target)
        {
            if (target == eaTarget) return;
            if (eaTarget)
            {
                eaProgress *= 1f - eaReacquireReset;
                eaReacquires++;
            }
            else eaProgress = 0f;
            eaTarget = target;
            eaHaveVel = false;
            eaHadLos = true;
            eaJinkArmed = true;
            eaLastBoostCd = target ? target.BoostCooldownRemaining : 0f;
        }

        private static int AsteroidMask => 1 << Substrate.LayerIds.Asteroid;

        /// <summary>Earned accuracy: a bearing error of RMS E0 x (1 - progress)^curve, where progress grows over eaT seconds of tracking (target inside the nose cone, in range, line of sight clear) and the target's jinks, boosts and broken line of sight knock it back.</summary>
        private void EarnedAccuracy(Vector2 selfPos, Vector2 tPos, Vector2 tVel, ref Vector2 pos, ref Vector2 vel)
        {
            var now = Time.fixedTimeAsDouble;
            if (now != eaLastTime)
            {
                var dt = double.IsNaN(eaLastTime) ? 0f : Mathf.Min((float)(now - eaLastTime), 0.1f);
                eaLastTime = now;
                var los = tPos - selfPos;
                var dist = los.magnitude;
                var losHat = dist > 1e-4f ? los / dist : Vector2.up;
                if (eaHaveVel && dt > 0f)
                    eaAccel = Vector2.Lerp(eaAccel, (tVel - eaLastVel) / dt, Mathf.Clamp01(dt / Mathf.Max(eaAccelSmooth, dt)));
                eaLastVel = tVel;
                eaHaveVel = true;
                eaLatAccel = Mathf.Abs(eaAccel.x * losHat.y - eaAccel.y * losHat.x);
                if (eaJinkAccel > 0f)
                {
                    if (eaJinkArmed && eaLatAccel > eaJinkAccel)
                    {
                        eaJinkArmed = false;
                        eaProgress *= 1f - eaJinkReset;
                        eaJinks++;
                    }
                    else if (!eaJinkArmed && eaLatAccel < 0.5f * eaJinkAccel) eaJinkArmed = true;
                }
                if (eaBoostReset > 0f && eaTarget)
                {
                    var cd = eaTarget.BoostCooldownRemaining;
                    if (cd > eaLastBoostCd + 0.5f)
                    {
                        eaProgress *= 1f - eaBoostReset;
                        eaBoosts++;
                    }
                    eaLastBoostCd = cd;
                }
                var losClear = true;
                if (eaLosReset > 0f)
                {
                    losClear = !Physics.Linecast(Substrate.GamePlane.PlanePointToWorld(selfPos),
                        Substrate.GamePlane.PlanePointToWorld(tPos), AsteroidMask);
                    if (!losClear && eaHadLos)
                    {
                        eaProgress *= 1f - eaLosReset;
                        eaLosBreaks++;
                    }
                    eaHadLos = losClear;
                }
                var fwd = ship ? ship.Kinematics.Forward : losHat;
                var inCone = Vector2.Angle(fwd, los) <= eaCone;
                var inRange = eaRange <= 0f || dist <= eaRange;
                var rate = dt / Mathf.Max(eaT, 1e-3f);
                if (inCone && inRange && losClear) eaProgress += rate;
                else if (!inCone) eaProgress -= eaLoseRate * rate;
                eaProgress = Mathf.Clamp01(eaProgress);

                eaRng ??= new System.Random(eaSeed);
                if (dt > 0f)
                {
                    var u1 = 1.0 - eaRng.NextDouble();
                    var u2 = eaRng.NextDouble();
                    var gauss = (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
                    eaW += -eaW / eaNoiseTau * dt + Mathf.Sqrt(2f * dt / eaNoiseTau) * gauss;
                }
                eaAngle = eaE0 * Mathf.Pow(1f - eaProgress, Mathf.Max(0.01f, eaCurve)) * eaW;
            }
            Rotate(selfPos, eaAngle, ref pos, ref vel);
        }

        private static void Rotate(Vector2 center, float deg, ref Vector2 pos, ref Vector2 vel)
        {
            var c = Mathf.Cos(deg * Mathf.Deg2Rad);
            var sn = Mathf.Sin(deg * Mathf.Deg2Rad);
            var rel = pos - center;
            pos = center + new Vector2(rel.x * c - rel.y * sn, rel.x * sn + rel.y * c);
            vel = new Vector2(vel.x * c - vel.y * sn, vel.x * sn + vel.y * c);
        }

        /// <summary>Skill dial s in [0,1]: scales the aim controller's acceleration and the strafe dodge; perception error is applied in Perceive.</summary>
        public void ApplySkill()
        {
            if (skill < 0f) return;
            var s = Mathf.Clamp01(skill);
            aimAccelFrac = Mathf.Lerp(skillAccelMin, aimAccelFrac, 1f - SkillError);
            strafeFraction *= s;
            var widen = 1f + skillTolGain * SkillError;
            geomTolScale *= widen;
            if (chargeTolScale > 0f) chargeTolScale *= widen;
            if (railAbortErr > 0f) railAbortErr *= widen;
        }

        /// <summary>Error scale (1-s)^curve: 1 at s = 0, 0 at s = 1.</summary>
        private float SkillError => Mathf.Pow(1f - Mathf.Clamp01(skill), Mathf.Max(0.01f, skillCurve));

        /// <summary>The target as a skill-limited pilot sees it: delayed by SkillError x reaction and rotated about the shooter by a correlated bearing error.</summary>
        public void Perceive(Vector2 selfPos, ref Vector2 pos, ref Vector2 vel)
        {
            var truePos = pos;
            var trueVel = vel;
            if (skill >= 0f) PerceiveSkill(selfPos, ref pos, ref vel);
            if (eaE0 > 0f) EarnedAccuracy(selfPos, truePos, trueVel, ref pos, ref vel);
        }

        private void PerceiveSkill(Vector2 selfPos, ref Vector2 pos, ref Vector2 vel)
        {
            var now = Time.fixedTimeAsDouble;
            var delay = SkillError * skillReactionMax;
            perceived.Add((now, pos, vel));
            while (perceived.Count > 1 && perceived[1].t <= now - delay - 1e-6) perceived.RemoveAt(0);
            if (delay > 0f)
            {
                pos = perceived[0].pos;
                vel = perceived[0].vel;
            }

            var sigma = SkillError * skillNoiseMax;
            if (sigma <= 0f) return;
            noiseRng ??= new System.Random(skillSeed);
            if (now != lastNoiseTime)
            {
                var dt = Time.fixedDeltaTime;
                var u1 = 1.0 - noiseRng.NextDouble();
                var u2 = noiseRng.NextDouble();
                var gauss = (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
                noiseAngle += -noiseAngle / skillNoiseTau * dt + sigma * Mathf.Sqrt(2f * dt / skillNoiseTau) * gauss;
                lastNoiseTime = now;
            }
            var c = Mathf.Cos(noiseAngle * Mathf.Deg2Rad);
            var sn = Mathf.Sin(noiseAngle * Mathf.Deg2Rad);
            var rel = pos - selfPos;
            pos = selfPos + new Vector2(rel.x * c - rel.y * sn, rel.x * sn + rel.y * c);
            vel = new Vector2(vel.x * c - vel.y * sn, vel.x * sn + vel.y * c);
        }

        /// <summary>Grenades back-off: inside its own blast (plus margin) the pilot retreats straight away at full speed.</summary>
        public Vector2 Backoff(Vector2 law, in Kinematics self, in Kinematics enemy, float fullSpeed)
        {
            if (!grenadeBackoff || !ship || !ship.Weapons || !(ship.Weapons.Primary is Grenades g)) return law;
            var los = enemy.pos - self.pos;
            var r = los.magnitude;
            if (r >= g.BlastRadius + Mathf.Max(0f, grenadeSafeMargin) + grenadeBackoffExtra) return law;
            return (r > 1e-4f ? -los / r : Vector2.up) * fullSpeed;
        }

        private static float BearingRateDeg(in Kinematics self, Vector2 targetPos, Vector2 targetVel)
        {
            var r = targetPos - self.pos;
            var v = targetVel - self.vel;
            return r.sqrMagnitude > 1e-4f ? (r.x * v.y - r.y * v.x) / r.sqrMagnitude * Mathf.Rad2Deg : 0f;
        }

        private bool OrbitAmbush(in Kinematics self, Vector2 targetPos, Vector2 targetVel)
            => railOrbit && Mathf.Abs(BearingRateDeg(in self, targetPos, targetVel)) > railOrbitRate;

        /// <summary>Target position after t seconds under constant range rate and bearing rate about the shooter.</summary>
        private static Vector2 PolarPredict(in Kinematics self, Vector2 targetPos, Vector2 targetVel, float t)
        {
            var r = targetPos - self.pos;
            var dist = r.magnitude;
            if (dist < 1e-3f) return targetPos;
            var v = targetVel - self.vel;
            var omega = (r.x * v.y - r.y * v.x) / (dist * dist);
            var rdot = Vector2.Dot(r, v) / dist;
            var c = Mathf.Cos(omega * t);
            var s = Mathf.Sin(omega * t);
            var dir = r / dist;
            return self.pos + new Vector2(dir.x * c - dir.y * s, dir.x * s + dir.y * c) * Mathf.Max(0.5f, dist + rdot * t);
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
            if (dist > EffectiveFireRange(weapon) || !weapon.CanFire()) return false;
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
            var tol = chargeTolScale > 0f
                ? Mathf.Max(1f, Mathf.Atan2(targetRadius * chargeTolScale, Mathf.Max(dist, 0.1f)) * Mathf.Rad2Deg)
                : Tolerance(weapon, dist, targetRadius, 5f);
            var ready = !cooldown || cooldown.CanFire();
            var inRange = dist <= EffectiveFireRange(weapon);
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
            if (pct <= 0f && OrbitAmbush(in self, targetPos, targetVel))
                return inRange && AimErr(in self, PolarPredict(in self, targetPos, targetVel, charge.FullChargeTime)) <= railOrbitCommit;
            if (pct <= 0f) return inRange && errAtFire <= railCommitCone;
            if (!inRange) return false;
            var nextPct = pct + Time.fixedDeltaTime / charge.FullChargeTime;
            if (railAbortErr > 0f && nextPct >= 0.999f)
            {
                var tol = railTolScale > 0f
                    ? Mathf.Max(0.5f, Mathf.Atan2(targetRadius * railTolScale, Mathf.Max(dist, 0.1f)) * Mathf.Rad2Deg)
                    : Mathf.Max(railAbortErr, Tolerance(weapon, dist, targetRadius, 2f));
                if (AimErr(in self, targetPos) > tol) return false;
                if (railMaxTargetBank > 0f && Mathf.Abs(targetBank) > railMaxTargetBank) return false;
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
            if (grenadeChainGap >= 0f && !grenadeHooked)
            {
                grenadeHooked = true;
                weapon.OnFire += OnGrenadeFired;
            }
            if (!weapon.CanFire()) return false;
            if (grenadeChainGap >= 0f && Time.fixedTimeAsDouble < nextGrenadeOk) return false;
            var aim = GrenadeAim(weapon, in self, targetPos, targetVel);
            var d = Vector2.Distance(self.pos, aim);
            lastGrenadeAimDist = d;
            if (grenadeSafeMargin >= 0f)
            {
                var safe = weapon.BlastRadius + grenadeSafeMargin;
                if (Vector2.Distance(self.pos, targetPos) < safe) return false;
                if (Vector2.Distance(self.pos + self.vel * (d / 20f), aim) < safe) return false;
            }
            return d >= grenadeMinRange && d <= grenadeMaxRange && AimErr(in self, aim) <= grenadeCone;
        }

        // The next charge waits until this one has landed and its wave has finished expanding, so it never flies into that wave.
        private void OnGrenadeFired()
        {
            var wave = ship && ship.Weapons && ship.Weapons.Primary is Grenades g ? g.BlastRadius / 15f : 0.8f;
            nextGrenadeOk = Time.fixedTimeAsDouble + lastGrenadeAimDist / 20f + wave + grenadeChainGap;
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
                    if (OrbitAmbush(in self, targetPos, targetVel))
                        return r.Charge.ChargePct <= 0f
                            ? self.pos + self.Forward * 10f
                            : PolarPredict(in self, targetPos, targetVel, remaining);
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
            if (railSteady > 0f && ship && ship.Weapons && ship.Weapons.Primary is Railguns steadyRail
                && steadyRail.Charge.ChargePct > 0f
                && (1f - steadyRail.Charge.ChargePct) * steadyRail.Charge.FullChargeTime <= railSteady)
                cmd.strafe = 0f;
            if (hardJink && hasTarget && evadeUrgency <= 0f && strafePeriod > 0f)
                cmd.strafe = Mathf.FloorToInt(Time.fixedTime / strafePeriod) % 2 == 1 ? -1f : 1f;
            if (!aimLock || !hasTarget || evadeUrgency > 0f) return;
            if (aimAccelFF)
            {
                if (haveLastVel)
                    targetAccel = Vector2.Lerp(targetAccel, (targetVel - lastTargetVel) / Time.fixedDeltaTime, accelSmoothing);
                lastTargetVel = targetVel;
                haveLastVel = true;
            }
            var aim = AimPoint(in self, targetPos, targetVel);
            if (!aim.HasValue) return;
            var r = aim.Value - self.pos;
            if (r.sqrMagnitude < 0.01f) return;
            var desiredYaw = Mathf.Atan2(-r.x, r.y) * Mathf.Rad2Deg;
            var err = Mathf.DeltaAngle(self.yaw, desiredYaw);
            var v = targetVel - self.vel;
            var ambush = ship.Weapons.Primary is Railguns && OrbitAmbush(in self, targetPos, targetVel);
            var bearingRate = ambush ? 0f : (r.x * v.y - r.y * v.x) / r.sqrMagnitude * Mathf.Rad2Deg;
            if (aimAccelFF && !ambush)
            {
                // Lead the rate command by the target's lateral acceleration over the controller's lag.
                var bearingAccel = (r.x * targetAccel.y - r.y * targetAccel.x) / r.sqrMagnitude * Mathf.Rad2Deg;
                bearingRate += bearingAccel * aimTau * 2f;
            }
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
