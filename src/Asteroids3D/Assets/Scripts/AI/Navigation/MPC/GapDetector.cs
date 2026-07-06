using AI.Scanning;
using Unity.Mathematics;

namespace Movement.MPC
{
    /// <summary>How a gap can be threaded given the ship's bank envelope.</summary>
    public enum GapClass : byte
    {
        /// <summary>Wider than the ship diameter — passable un-banked.</summary>
        Open,
        /// <summary>Only passable while banked (hull narrowed by cos(maxBank)).</summary>
        BankOnly,
    }

    /// <summary>A free angular corridor around the ship between blocking obstacles.</summary>
    public struct Gap
    {
        public float dirRad;        // chosen heading through the gap (MPC yaw convention: atan2(-x, y))
        public float edgeLoRad;     // free-interval boundaries (for gizmos), MPC yaw convention
        public float edgeHiRad;
        public float angularWidth;  // radians
        public float linearWidth;   // world units, surface-to-surface between the two wall obstacles
        public float mouthDist;     // distance along dirRad to the gap mouth (wall plane)
        public float depth;         // clear distance along dirRad to the next obstacle
        public float score;
        public GapClass classification;
        public float2 leftWall;     // bounding obstacle centers (for gizmos); zero if unbounded
        public float2 rightWall;
    }

    /// <summary>
    /// Analytic egocircle/disparity gap finder over an <see cref="ObstacleScan"/>. Plain C# — does
    /// NOT touch Scout/ObstacleScanner. Marks the angular arc each (bank-inflated) obstacle blocks
    /// on a discretized egocircle, then reads the free runs between blockers as candidate gaps,
    /// classifies them (open / bank-only / impassable-discarded), scores by goal alignment + width
    /// + depth, and returns the top-k. Reusable instance (fixed scratch buffers, no per-call GC).
    /// </summary>
    public sealed class GapDetector
    {
        public const int Bins = 180;                 // 2° angular resolution
        private const float TwoPi = 2f * math.PI;
        private const float BinSize = TwoPi / Bins;

        // Scoring weights (alignment dominates: chase toward the goal).
        private const float WAlign = 0.6f;
        private const float WWidth = 0.25f;
        private const float WDepth = 0.15f;
        private const float MinAlign = -0.2f;        // discard gaps facing well away from the goal

        private readonly bool[] blocked = new bool[Bins];
        private readonly int[] blockerObs = new int[Bins];
        private readonly float[] blockerDist = new float[Bins];

        /// <summary>
        /// Detect free gaps around <paramref name="pos"/>. Writes up to <paramref name="maxGaps"/>
        /// top-scored gaps into <paramref name="outGaps"/> and returns the count.
        /// </summary>
        public int Detect(float2 pos, float2 goalDir,
            ObstacleScan scan, float shipRadius, float maxBankAngleRad, float safetyMargin,
            float workingRange, Gap[] outGaps, int maxGaps)
        {
            var count = scan.count;
            var hullBank = shipRadius * math.cos(maxBankAngleRad);
            var goalAng = Ang(goalDir);

            for (var b = 0; b < Bins; b++)
            {
                blocked[b] = false;
                blockerObs[b] = -1;
                blockerDist[b] = float.MaxValue;
            }

            var anyBlocked = false;
            for (var i = 0; i < count; i++)
            {
                var obs = scan.buffer[i];
                var toObs = new float2(obs.position.x, obs.position.y) - pos;
                var d = math.length(toObs);
                if (d < 1e-3f || d > workingRange) continue;

                // Block the obstacle's true angular silhouette (not hull-inflated) so a bank-only
                // opening still shows angular clearance; passability is decided by linear width below.
                var arg = obs.radius / d;
                var half = arg >= 1f ? math.PI : math.asin(arg);
                MarkArc(Ang(toObs), half, i, d);
                anyBlocked = true;
            }

            var written = 0;
            if (!anyBlocked)
            {
                // Whole egocircle free — a single open gap straight toward the goal.
                outGaps[0] = new Gap
                {
                    dirRad = goalAng,
                    edgeLoRad = goalAng - math.PI,
                    edgeHiRad = goalAng + math.PI,
                    angularWidth = TwoPi,
                    linearWidth = float.MaxValue,
                    depth = workingRange,
                    score = 1f,
                    classification = GapClass.Open,
                };
                return 1;
            }

            // Walk the circular bin array from a blocked bin, reading free runs.
            var startBlocked = 0;
            while (startBlocked < Bins && !blocked[startBlocked]) startBlocked++;
            if (startBlocked >= Bins) return 0; // fully blocked

            var runStart = -1;
            for (var step = 1; step <= Bins; step++)
            {
                var b = (startBlocked + step) % Bins;
                if (!blocked[b])
                {
                    if (runStart < 0) runStart = b;
                }
                else if (runStart >= 0)
                {
                    var runEnd = (b - 1 + Bins) % Bins;
                    if (TryBuildGap(pos, goalDir, goalAng, runStart, runEnd,
                            shipRadius, hullBank, safetyMargin, workingRange, scan, out var gap))
                        InsertTopK(outGaps, ref written, maxGaps, gap);
                    runStart = -1;
                }
            }

            return written;
        }

        // Marks the arc [center - half, center + half] as blocked, recording the nearest blocker per bin.
        private void MarkArc(float center, float half, int obsIndex, float dist)
        {
            var centerBin = AngToBin(center);
            var span = (int)math.ceil(half / BinSize);
            for (var k = -span; k <= span; k++)
            {
                var b = ((centerBin + k) % Bins + Bins) % Bins;
                blocked[b] = true;
                if (dist < blockerDist[b])
                {
                    blockerDist[b] = dist;
                    blockerObs[b] = obsIndex;
                }
            }
        }

        private bool TryBuildGap(float2 pos, float2 goalDir, float goalAng,
            int runStart, int runEnd, float shipRadius, float hullBank, float safetyMargin,
            float workingRange, ObstacleScan scan, out Gap gap)
        {
            gap = default;

            var runBins = (runEnd - runStart + Bins) % Bins + 1;
            var angularWidth = runBins * BinSize;

            var leftIdx = blockerObs[(runStart - 1 + Bins) % Bins];   // wall just before the run
            var rightIdx = blockerObs[(runEnd + 1) % Bins];           // wall just after the run

            var edgeLo = BinToAng(runStart) - 0.5f * BinSize;
            var edgeHi = BinToAng(runEnd) + 0.5f * BinSize;

            // Direction through the gap: bias to the goal bearing if it falls inside the free run,
            // else the run's bisector.
            var center = edgeLo + 0.5f * AngDelta(edgeHi, edgeLo);
            var dir = AngWithin(goalAng, edgeLo, edgeHi) ? goalAng : center;
            var dirVec = new float2(-math.sin(dir), math.cos(dir));

            var align = math.dot(dirVec, goalDir);
            if (align < MinAlign) return false;

            // Linear (surface-to-surface) width between the two wall obstacles, and the distance
            // along the gap axis to the mouth (the wall plane).
            float linearWidth;
            var mouthDist = workingRange;
            float2 leftWall = default, rightWall = default;
            if (leftIdx < 0 || rightIdx < 0)
            {
                linearWidth = float.MaxValue; // unbounded on one side
            }
            else
            {
                var a = scan.buffer[leftIdx];
                var b = scan.buffer[rightIdx];
                leftWall = new float2(a.position.x, a.position.y);
                rightWall = new float2(b.position.x, b.position.y);
                linearWidth = math.max(0f, math.distance(leftWall, rightWall) - a.radius - b.radius);
                mouthDist = math.max(0f, 0.5f * (math.dot(leftWall - pos, dirVec) + math.dot(rightWall - pos, dirVec)));
            }

            var shipDiameter = 2f * shipRadius;
            var bankDiameter = 2f * hullBank;
            var margin = 2f * safetyMargin;
            GapClass cls;
            if (linearWidth > shipDiameter + margin) cls = GapClass.Open;
            else if (linearWidth > bankDiameter + margin) cls = GapClass.BankOnly;
            else return false; // impassable even banked

            var depth = RayDepth(pos, dir, scan, hullBank, workingRange);

            var widthNorm = math.saturate((linearWidth - bankDiameter) / math.max(shipDiameter, 1e-3f));
            var depthNorm = math.saturate(depth / math.max(workingRange, 1e-3f));
            var score = WAlign * math.saturate(align) + WWidth * widthNorm + WDepth * depthNorm;

            gap = new Gap
            {
                dirRad = dir,
                edgeLoRad = edgeLo,
                edgeHiRad = edgeHi,
                angularWidth = angularWidth,
                linearWidth = linearWidth,
                mouthDist = mouthDist,
                depth = depth,
                score = score,
                classification = cls,
                leftWall = leftWall,
                rightWall = rightWall,
            };
            return true;
        }

        // Distance along dir to the nearest (bank-inflated) obstacle surface, capped at workingRange.
        private static float RayDepth(float2 pos, float dir, ObstacleScan scan, float hullBank, float workingRange)
        {
            var d = new float2(-math.sin(dir), math.cos(dir));
            var best = workingRange;
            for (var i = 0; i < scan.count; i++)
            {
                var obs = scan.buffer[i];
                var oc = new float2(obs.position.x, obs.position.y) - pos;
                var tca = math.dot(oc, d);
                if (tca < 0f) continue;
                var rr = obs.radius + hullBank;
                var perpSq = math.lengthsq(oc) - tca * tca;
                if (perpSq > rr * rr) continue;
                var t = tca - math.sqrt(rr * rr - perpSq);
                if (t > 0f && t < best) best = t;
            }
            return best;
        }

        private static void InsertTopK(Gap[] gaps, ref int written, int maxGaps, Gap gap)
        {
            if (written < maxGaps)
            {
                var j = written++;
                while (j > 0 && gaps[j - 1].score < gap.score) { gaps[j] = gaps[j - 1]; j--; }
                gaps[j] = gap;
                return;
            }
            if (gap.score <= gaps[maxGaps - 1].score) return;
            var i = maxGaps - 1;
            while (i > 0 && gaps[i - 1].score < gap.score) { gaps[i] = gaps[i - 1]; i--; }
            gaps[i] = gap;
        }

        // ── angle helpers (MPC yaw convention: yaw = atan2(-x, y), forward = (-sin, cos)) ──
        private static float Ang(float2 v) => math.atan2(-v.x, v.y);

        private static int AngToBin(float ang)
        {
            var a = ang < 0f ? ang + TwoPi : ang;
            return ((int)math.round(a / BinSize)) % Bins;
        }

        private static float BinToAng(int bin) => bin * BinSize; // [0, 2π)

        // Smallest signed a-b wrapped to (-π, π].
        private static float AngDelta(float a, float b)
        {
            var d = a - b;
            while (d > math.PI) d -= TwoPi;
            while (d <= -math.PI) d += TwoPi;
            return d;
        }

        // Is angle x inside the CCW arc [lo, hi] (in bin-space, hi may exceed lo by up to 2π)?
        private static bool AngWithin(float x, float lo, float hi)
        {
            var norm = Norm(x);
            var l = Norm(lo);
            var h = Norm(hi);
            if (l <= h) return norm >= l && norm <= h;
            return norm >= l || norm <= h; // wrapped
        }

        private static float Norm(float a)
        {
            a %= TwoPi;
            if (a < 0f) a += TwoPi;
            return a;
        }
    }
}
