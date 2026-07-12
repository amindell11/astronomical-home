using System;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One maneuver-oracle episode's configuration. Mirrors ChaseRunConfig's shape.</summary>
    [Serializable]
    public struct OracleRunConfig
    {
        public ManeuverKind maneuver;
        public float wVelTrack;
        public float orbitRadius;
        public float desiredRange;
        public float orbitSpeedFraction;
        public bool freeYaw;
        public float aimRateDegPerSec;
        public bool useField;
        public float duration;
        public string tag;
    }

    /// <summary>One JSONL row: config echo plus per-maneuver metrics and pass flags. JsonUtility-serializable.</summary>
    [Serializable]
    public struct OracleRunResult
    {
        public string schema;
        public string tag;
        public string maneuver;
        public float wVelTrack;
        public float orbitSpeedFraction;
        public bool freeYaw;
        public float duration;
        public bool useField;

        public float meanSpeed;
        public int radiusSampleCount;

        public float meanRadiusErrFrac;
        public float angularProgressRad;
        public bool orbitPass;

        public float timeToExceedArc;
        public float heldOutFraction;
        public bool breakPass;

        public float settleTime;
        public float steadyStateErr;
        public float finalWindowVariance;
        public bool rangePass;

        public const string SchemaId = "maneuver-oracle-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>
    /// Ship-vs-dummy geometry accumulator, fed once per FixedUpdate by the test driver. Turns the
    /// per-tick series into the envelope metrics the gate reads (radius hold + angular progress for
    /// orbit, arc breakout for break, settle + steady-state error for range).
    /// </summary>
    public class OracleMetrics
    {
        private const float SettleSeconds = 3f;
        private const float FinalWindowSeconds = 2f;
        private const float ArcHalfWidthDeg = 20f;
        private const float OrbitRadiusErrFracThreshold = 0.20f;

        private readonly OracleRunConfig config;
        private readonly float rangeTolerance;

        private float time;
        private int sampleCount;
        private float speedSum;

        private bool hasPrevAngle;
        private float prevLosAngle;
        private float angularProgress;

        private float radiusErrSum;
        private int radiusSettleCount;

        private float timeToExceedArc = -1f;
        private int postExceedCount;
        private int postExceedHeldOut;

        private float settleCandidate = -1f;
        private float finalWindowRSum;
        private float finalWindowRSqSum;
        private int finalWindowCount;

        public int RadiusSampleCount => radiusSettleCount;
        public float AngularProgress => angularProgress;

        public OracleMetrics(OracleRunConfig config)
        {
            this.config = config;
            rangeTolerance = Mathf.Max(2f, 0.1f * config.desiredRange);
        }

        public void Sample(Vector2 shipPos, Vector2 shipVel, Vector2 dummyPos, Vector2 dummyForward, float dt)
        {
            time += dt;
            sampleCount++;
            speedSum += shipVel.magnitude;

            var los = shipPos - dummyPos;
            var r = los.magnitude;

            var losAngle = Mathf.Atan2(los.y, los.x);
            if (hasPrevAngle)
                angularProgress += Mathf.DeltaAngle(prevLosAngle * Mathf.Rad2Deg, losAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            prevLosAngle = losAngle;
            hasPrevAngle = true;

            if (time >= SettleSeconds)
            {
                radiusErrSum += Mathf.Abs(r - config.orbitRadius);
                radiusSettleCount++;
            }

            SampleBreak(r > 1e-4f ? los / r : Vector2.up, dummyForward);
            SampleRange(r);
        }

        private void SampleBreak(Vector2 dummyToShipHat, Vector2 dummyForward)
        {
            var fwd = dummyForward.sqrMagnitude > 1e-6f ? dummyForward.normalized : Vector2.up;
            var cos = Mathf.Clamp(Vector2.Dot(fwd, dummyToShipHat), -1f, 1f);
            var exposureDeg = Mathf.Acos(cos) * Mathf.Rad2Deg;
            var out_ = exposureDeg > ArcHalfWidthDeg;

            if (out_ && timeToExceedArc < 0f) timeToExceedArc = time;
            if (timeToExceedArc >= 0f)
            {
                postExceedCount++;
                if (out_) postExceedHeldOut++;
            }
        }

        private void SampleRange(float r)
        {
            var err = Mathf.Abs(r - config.desiredRange);
            if (err < rangeTolerance)
            {
                if (settleCandidate < 0f) settleCandidate = time;
            }
            else settleCandidate = -1f;

            if (time >= config.duration - FinalWindowSeconds)
            {
                finalWindowRSum += r;
                finalWindowRSqSum += r * r;
                finalWindowCount++;
            }
        }

        public OracleRunResult Finalize()
        {
            var meanSpeed = sampleCount > 0 ? speedSum / sampleCount : 0f;
            var meanRadiusErrFrac = (radiusSettleCount > 0 && config.orbitRadius > 1e-4f)
                ? (radiusErrSum / radiusSettleCount) / config.orbitRadius : float.NaN;
            var heldOutFraction = postExceedCount > 0 ? (float)postExceedHeldOut / postExceedCount : 0f;

            var finalMeanR = finalWindowCount > 0 ? finalWindowRSum / finalWindowCount : 0f;
            var finalVar = finalWindowCount > 0
                ? Mathf.Max(0f, finalWindowRSqSum / finalWindowCount - finalMeanR * finalMeanR) : float.NaN;
            var steadyStateErr = finalWindowCount > 0 ? Mathf.Abs(finalMeanR - config.desiredRange) : float.NaN;

            var orbitPass = !float.IsNaN(meanRadiusErrFrac)
                && meanRadiusErrFrac < OrbitRadiusErrFracThreshold
                && Mathf.Abs(angularProgress) >= 2f * Mathf.PI;
            var breakPass = timeToExceedArc >= 0f && timeToExceedArc <= 2.5f && heldOutFraction > 0.8f;
            var rangePass = settleCandidate >= 0f
                && !float.IsNaN(steadyStateErr) && steadyStateErr < rangeTolerance
                && !float.IsNaN(finalVar) && finalVar < rangeTolerance * rangeTolerance;

            return new OracleRunResult
            {
                schema = OracleRunResult.SchemaId,
                tag = config.tag,
                maneuver = config.maneuver.ToString(),
                wVelTrack = config.wVelTrack,
                orbitSpeedFraction = config.orbitSpeedFraction,
                freeYaw = config.freeYaw,
                duration = time,
                useField = config.useField,
                meanSpeed = meanSpeed,
                radiusSampleCount = radiusSettleCount,
                meanRadiusErrFrac = meanRadiusErrFrac,
                angularProgressRad = angularProgress,
                orbitPass = orbitPass,
                timeToExceedArc = timeToExceedArc,
                heldOutFraction = heldOutFraction,
                breakPass = breakPass,
                settleTime = settleCandidate,
                steadyStateErr = steadyStateErr,
                finalWindowVariance = finalVar,
                rangePass = rangePass,
            };
        }
    }
}
