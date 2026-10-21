using System.Collections.Generic;
using Movement;
using Ships;
using Substrate;
using UnityEngine;

namespace AI.Playtest
{
    /// <summary>Throwaway playtest port of the research GunDoctrine's skill dial and earned accuracy: the target as the Gunner perceives it. Both trackers run every tick so a live preset switch lands on warm state; only the active preset's distortion is applied.</summary>
    public sealed class PlaytestPerception
    {
        private const float MaxDelay = LethalityPresets.DialReactionMax;

        private readonly int seed;
        private readonly List<(double t, Vector2 pos, Vector2 vel)> history = new();
        private System.Random dialRng;
        private float dialW;
        private double dialLastTime;

        private System.Random eaRng;
        private Ship eaTarget;
        private float eaProgress;
        private float eaW;
        private float eaAngle;
        private double eaLastTime;
        private Vector2 eaLastVel, eaAccel;
        private bool eaHaveVel;
        private bool eaJinkArmed;
        private bool eaHadLos;
        private float eaLastBoostCd;

        public PlaytestPerception(int seed)
        {
            this.seed = seed;
            Reset();
        }

        public void Reset()
        {
            history.Clear();
            dialRng = new System.Random(seed);
            dialW = 0f;
            dialLastTime = double.NaN;
            eaRng = new System.Random(seed ^ 0x5bd1e995);
            eaTarget = null;
            eaProgress = 0f;
            eaW = 0f;
            eaAngle = 0f;
            eaLastTime = double.NaN;
            eaHaveVel = false;
            eaAccel = Vector2.zero;
            eaJinkArmed = true;
            eaHadLos = true;
            eaLastBoostCd = 0f;
        }

        /// <summary>The Gunner's anchor ship this tick: a new ship, or a switch away from a live one, restarts earned progress.</summary>
        public void NoteTarget(Ship target)
        {
            if (target == eaTarget) return;
            eaProgress = 0f;
            eaTarget = target;
            eaHaveVel = false;
            eaHadLos = true;
            eaJinkArmed = true;
            eaLastBoostCd = target ? target.BoostCooldownRemaining : 0f;
        }

        public void Perceive(in Kinematics self, ref Vector2 pos, ref Vector2 vel)
        {
            var truePos = pos;
            var trueVel = vel;
            Dial(self.pos, ref pos, ref vel);
            EarnedAccuracy(in self, truePos, trueVel, ref pos, ref vel);
        }

        private void Dial(Vector2 selfPos, ref Vector2 pos, ref Vector2 vel)
        {
            var now = Time.fixedTimeAsDouble;
            history.Add((now, pos, vel));
            while (history.Count > 1 && history[1].t <= now - MaxDelay - 1e-6) history.RemoveAt(0);
            if (now != dialLastTime)
            {
                dialLastTime = now;
                var dt = Time.fixedDeltaTime;
                dialW += -dialW / LethalityPresets.DialNoiseTau * dt
                    + Mathf.Sqrt(2f * dt / LethalityPresets.DialNoiseTau) * Gauss(dialRng);
            }

            var skill = LethalityPresets.Skill;
            if (skill < 0f) return;
            var error = 1f - Mathf.Clamp01(skill);

            var delay = error * LethalityPresets.DialReactionMax;
            if (delay > 0f)
            {
                var i = 0;
                while (i + 1 < history.Count && history[i + 1].t <= now - delay - 1e-6) i++;
                pos = history[i].pos;
                vel = history[i].vel;
            }

            var sigma = error * LethalityPresets.DialNoiseMaxDeg;
            if (sigma > 0f) Rotate(selfPos, sigma * dialW, ref pos, ref vel);
        }

        private static int AsteroidMask => 1 << LayerIds.Asteroid;

        private void EarnedAccuracy(in Kinematics self, Vector2 tPos, Vector2 tVel, ref Vector2 pos, ref Vector2 vel)
        {
            var selfPos = self.pos;
            var now = Time.fixedTimeAsDouble;
            if (now != eaLastTime)
            {
                var dt = double.IsNaN(eaLastTime) ? 0f : Mathf.Min((float)(now - eaLastTime), 0.1f);
                eaLastTime = now;
                var los = tPos - selfPos;
                var dist = los.magnitude;
                var losHat = dist > 1e-4f ? los / dist : Vector2.up;
                if (eaHaveVel && dt > 0f)
                    eaAccel = Vector2.Lerp(eaAccel, (tVel - eaLastVel) / dt,
                        Mathf.Clamp01(dt / Mathf.Max(LethalityPresets.EaAccelSmooth, dt)));
                eaLastVel = tVel;
                eaHaveVel = true;
                var latAccel = Mathf.Abs(eaAccel.x * losHat.y - eaAccel.y * losHat.x);
                if (eaJinkArmed && latAccel > LethalityPresets.EaJinkAccel)
                {
                    eaJinkArmed = false;
                    eaProgress = 0f;
                }
                else if (!eaJinkArmed && latAccel < 0.5f * LethalityPresets.EaJinkAccel) eaJinkArmed = true;

                if (eaTarget)
                {
                    var cd = eaTarget.BoostCooldownRemaining;
                    if (cd > eaLastBoostCd + 0.5f) eaProgress = 0f;
                    eaLastBoostCd = cd;
                }

                var losClear = !Physics.Linecast(GamePlane.PlanePointToWorld(selfPos),
                    GamePlane.PlanePointToWorld(tPos), AsteroidMask);
                if (!losClear && eaHadLos) eaProgress = 0f;
                eaHadLos = losClear;

                var inCone = Vector2.Angle(self.Forward, los) <= LethalityPresets.EaConeDeg;
                var inRange = dist <= LethalityPresets.EaRange;
                if (inCone && inRange && losClear) eaProgress += dt / LethalityPresets.EaT;
                eaProgress = Mathf.Clamp01(eaProgress);

                if (dt > 0f)
                    eaW += -eaW / LethalityPresets.EaNoiseTau * dt
                        + Mathf.Sqrt(2f * dt / LethalityPresets.EaNoiseTau) * Gauss(eaRng);
                eaAngle = LethalityPresets.EaE0Deg * (1f - eaProgress) * eaW;
            }
            if (LethalityPresets.EarnedAccuracyOn) Rotate(selfPos, eaAngle, ref pos, ref vel);
        }

        private static float Gauss(System.Random rng)
        {
            var u1 = 1.0 - rng.NextDouble();
            var u2 = rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
        }

        private static void Rotate(Vector2 center, float deg, ref Vector2 pos, ref Vector2 vel)
        {
            var c = Mathf.Cos(deg * Mathf.Deg2Rad);
            var sn = Mathf.Sin(deg * Mathf.Deg2Rad);
            var rel = pos - center;
            pos = center + new Vector2(rel.x * c - rel.y * sn, rel.x * sn + rel.y * c);
            vel = new Vector2(vel.x * c - vel.y * sn, vel.x * sn + vel.y * c);
        }
    }
}
