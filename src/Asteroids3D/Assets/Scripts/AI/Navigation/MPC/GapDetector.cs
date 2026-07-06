using AI.Scanning;
using Unity.Mathematics;

namespace Movement.MPC
{
    /// <summary>
    /// A traversable angular gap between scanned obstacles, as seen from the ship.
    /// Angles use the MPC yaw convention (0 = +Y, bearing(v) = atan2(-v.x, v.y)).
    /// </summary>
    public struct DetectedGap
    {
        public float axisAngle;     // bearing of the gap center (rad)
        public float angularWidth;  // free angular width (rad)
        public float depth;         // distance to the nearer bounding obstacle surface (u)
        public bool bankOnly;       // free for the bank-narrowed hull but not the upright hull
        public float score;
    }

    /// <summary>
    /// Closed-form egocircle/disparity gap pass over the existing <see cref="ObstacleScan"/>
    /// circles (trade study §3.2): each obstacle blocks an angular arc inflated by the ship
    /// hull; the free arcs between merged blocked arcs are candidate gaps. Ran twice — with
    /// the upright hull and with the bank-narrowed hull — a gap free only under the narrowed
    /// hull is a "bank-only" gap (thread it by knifing at max strafe). Plain C#, no physics,
    /// no per-frame allocation. Owns the frame-to-frame gap hysteresis: the chosen gap only
    /// switches when a competitor beats it by a margin — oscillation is killed here, not
    /// inside the solver.
    /// </summary>
    public class GapDetector
    {
        private struct Arc
        {
            public float start, end; // radians; end > start; may exceed +π before wrapping
            public float depth;      // min surface distance among merged member obstacles
        }

        private const int MaxArcs = 256; // 128 scan entries × worst-case seam split
        private readonly Arc[] fullArcs = new Arc[MaxArcs];
        private readonly Arc[] bankArcs = new Arc[MaxArcs];
        private readonly Arc[] scratch = new Arc[MaxArcs];

        // Hysteresis state
        private bool hasChosen;
        private float chosenAxis;
        private const float GapAssociationAngle = 0.35f; // ~20° — same-gap tracking tolerance

        /// <summary>
        /// Finds free gaps between obstacles within maxRange. Returns the number written to
        /// <paramref name="results"/>. Open space with no obstacles yields zero gaps — there
        /// is nothing to thread.
        /// </summary>
        public int Detect(float2 shipPos, ObstacleScan scan,
            float hullFull, float hullBank, float maxRange, DetectedGap[] results)
        {
            var fullCount = BuildBlockedArcs(shipPos, scan, hullFull, maxRange, fullArcs);
            var bankCount = BuildBlockedArcs(shipPos, scan, hullBank, maxRange, bankArcs);
            if (bankCount < 0 || fullCount < 0) return 0; // ship inside an obstacle — no usable gaps

            fullCount = MergeArcs(fullArcs, fullCount);
            bankCount = MergeArcs(bankArcs, bankCount);
            if (bankCount == 0) return 0; // no obstacles → no gaps to propose

            // Free intervals of the bank set are the candidate gaps (superset of the full
            // set's). Classify each against the full set's free space.
            var gapCount = 0;
            for (var i = 0; i < bankCount && gapCount < results.Length; i++)
            {
                var arc = bankArcs[i];
                var next = bankArcs[(i + 1) % bankCount];
                var freeStart = arc.end;
                var freeEnd = next.start + (i == bankCount - 1 ? 2f * math.PI : 0f);
                var width = freeEnd - freeStart;
                if (width <= 1e-4f) continue;

                var gap = new DetectedGap
                {
                    angularWidth = width,
                    depth = math.min(arc.depth, next.depth),
                };

                // Widest sub-interval of this gap that is also free for the upright hull.
                if (WidestFreeSubInterval(fullArcs, fullCount, freeStart, freeEnd,
                        out var subMid, out _))
                {
                    gap.axisAngle = Cost.WrapRadians(subMid);
                    gap.bankOnly = false;
                }
                else
                {
                    gap.axisAngle = Cost.WrapRadians(freeStart + width * 0.5f);
                    gap.bankOnly = true;
                }

                results[gapCount++] = gap;
            }

            return gapCount;
        }

        /// <summary>
        /// Scores a gap for chase usefulness: alignment to the goal bearing (dominant), how
        /// comfortably wide it is, whether the ship can realistically turn into it in the
        /// time it takes to arrive, and a small penalty for needing max bank.
        /// </summary>
        public static float Score(in DetectedGap gap, float shipYaw, float speed,
            float goalBearing, float maxYawRate)
        {
            var alignment = 0.5f * (1f + math.cos(gap.axisAngle - goalBearing));   // 0..1
            var widthFactor = math.saturate(gap.angularWidth / 0.5f);              // full credit at ~30°

            // Turn admissibility: can we align the nose before we arrive at the gap?
            var turnTime = maxYawRate > 0f
                ? math.abs(Cost.WrapRadians(gap.axisAngle - shipYaw)) / maxYawRate
                : 0f;
            var timeToGap = gap.depth / math.max(speed, 1f);
            var feasibility = math.saturate(timeToGap / math.max(turnTime, 1e-3f));

            var bankFactor = gap.bankOnly ? 0.9f : 1f;
            return alignment * (0.25f + 0.75f * widthFactor) * math.max(feasibility, 0.2f) * bankFactor;
        }

        /// <summary>
        /// Chooses the gap to commit to, with hysteresis: the previously chosen gap (matched
        /// by axis proximity) is kept unless a competitor beats its score by switchMargin.
        /// Returns the chosen index, or -1 when count is 0 (also clears the tracking state).
        /// </summary>
        public int ChooseGap(DetectedGap[] gaps, int count, float switchMargin)
        {
            if (count == 0)
            {
                hasChosen = false;
                return -1;
            }

            var best = 0;
            for (var i = 1; i < count; i++)
                if (gaps[i].score > gaps[best].score) best = i;

            // Re-associate the previously chosen gap by angular proximity.
            var current = -1;
            if (hasChosen)
            {
                var currentErr = GapAssociationAngle;
                for (var i = 0; i < count; i++)
                {
                    var err = math.abs(Cost.WrapRadians(gaps[i].axisAngle - chosenAxis));
                    if (err < currentErr)
                    {
                        currentErr = err;
                        current = i;
                    }
                }
            }

            var chosen = current >= 0 && gaps[best].score <= gaps[current].score * (1f + switchMargin)
                ? current
                : best;

            hasChosen = true;
            chosenAxis = gaps[chosen].axisAngle;
            return chosen;
        }

        /// <summary>Bearing of the vector from the ship to a point, MPC yaw convention.</summary>
        public static float Bearing(float2 from, float2 to)
        {
            var d = to - from;
            return math.atan2(-d.x, d.y);
        }

        /// <summary>
        /// Writes the angular arcs blocked by each in-range obstacle (inflated by hullRadius)
        /// into <paramref name="arcs"/>, seam-split so every arc lies within [-π, 3π).
        /// Returns the arc count, or -1 if the ship overlaps an obstacle (no gap geometry).
        /// </summary>
        private static int BuildBlockedArcs(float2 shipPos, ObstacleScan scan,
            float hullRadius, float maxRange, Arc[] arcs)
        {
            var count = 0;
            for (var i = 0; i < scan.count && count < MaxArcs - 1; i++)
            {
                var obs = scan.buffer[i];
                var toObs = new float2(obs.position.x, obs.position.y) - shipPos;
                var dist = math.length(toObs);
                if (dist > maxRange) continue;

                var blockRadius = obs.radius + hullRadius;
                if (dist <= blockRadius + 1e-3f) return -1; // overlapping/touching this obstacle

                var bearing = math.atan2(-toObs.x, toObs.y);
                var halfAngle = math.asin(math.min(blockRadius / dist, 1f));
                var depth = dist - obs.radius;

                var start = bearing - halfAngle;
                var end = bearing + halfAngle;
                // Normalize start into [-π, π); the arc may then extend past +π, which the
                // circular merge handles by treating angles modulo 2π.
                if (start < -math.PI) { start += 2f * math.PI; end += 2f * math.PI; }
                if (start >= math.PI) { start -= 2f * math.PI; end -= 2f * math.PI; }

                arcs[count++] = new Arc { start = start, end = end, depth = depth };
            }
            return count;
        }

        /// <summary>
        /// Merges overlapping blocked arcs on the circle (in place). Returns the merged count;
        /// 0 means nothing blocked. After merging, arcs are sorted by start and disjoint; an
        /// arc may extend past +π (wraps onto the first arc's start).
        /// </summary>
        private int MergeArcs(Arc[] arcs, int count)
        {
            if (count <= 0) return math.max(count, 0);

            System.Array.Sort(arcs, 0, count, ArcComparer.Instance);

            var merged = 0;
            scratch[merged++] = arcs[0];
            for (var i = 1; i < count; i++)
            {
                ref var last = ref scratch[merged - 1];
                if (arcs[i].start <= last.end + 1e-5f)
                {
                    last.end = math.max(last.end, arcs[i].end);
                    last.depth = math.min(last.depth, arcs[i].depth);
                }
                else
                {
                    scratch[merged++] = arcs[i];
                }
            }

            // Wrap: if the last arc spills past +π onto the first arc, merge them.
            if (merged > 1)
            {
                ref var last = ref scratch[merged - 1];
                if (last.end - 2f * math.PI >= scratch[0].start - 1e-5f)
                {
                    scratch[0].start = last.start - 2f * math.PI;
                    scratch[0].end = math.max(scratch[0].end, last.end - 2f * math.PI);
                    scratch[0].depth = math.min(scratch[0].depth, last.depth);
                    merged--;
                }
            }

            System.Array.Copy(scratch, arcs, merged);
            return merged;
        }

        /// <summary>
        /// Finds the widest sub-interval of [freeStart, freeEnd] not covered by the given
        /// merged blocked arcs. Returns false if the whole interval is blocked.
        /// </summary>
        private static bool WidestFreeSubInterval(Arc[] blocked, int count,
            float freeStart, float freeEnd, out float mid, out float width)
        {
            mid = 0f;
            width = 0f;

            // Sweep: advance the cursor past every blocked arc (tested at ±2π offsets, since
            // the interval endpoints may sit outside [-π, π)) that intersects [cursor, freeEnd],
            // measuring each free run in between.
            var cursor = freeStart;
            for (var guard = 0; guard < count * 3 + 2 && cursor < freeEnd; guard++)
            {
                var nextBlockStart = freeEnd;
                var nextBlockEnd = freeEnd;
                for (var i = 0; i < count; i++)
                {
                    for (var k = -1; k <= 1; k++)
                    {
                        var s = blocked[i].start + k * 2f * math.PI;
                        var e = blocked[i].end + k * 2f * math.PI;
                        if (e <= cursor + 1e-6f || s >= freeEnd) continue;
                        if (s < nextBlockStart)
                        {
                            nextBlockStart = s;
                            nextBlockEnd = e;
                        }
                    }
                }

                var runEnd = math.min(nextBlockStart, freeEnd);
                var runWidth = runEnd - cursor;
                if (runWidth > width)
                {
                    width = runWidth;
                    mid = cursor + runWidth * 0.5f;
                }

                if (nextBlockEnd <= cursor) break; // no forward progress possible
                cursor = math.max(cursor, nextBlockEnd);
            }

            return width > 1e-3f;
        }

        private sealed class ArcComparer : System.Collections.Generic.IComparer<Arc>
        {
            public static readonly ArcComparer Instance = new();
            public int Compare(Arc a, Arc b) => a.start.CompareTo(b.start);
        }
    }

    /// <summary>
    /// Synthesizes scripted "bank through the gap" control sequences for CEM injection
    /// (Biased-MPPI pattern, trade study §3.3): align yaw to the gap axis → full thrust →
    /// a max-strafe bank pulse timed to the gap traversal → mirrored unwind pulse to cancel
    /// the lateral velocity. Variants differ in pulse sign and length so at least one lines
    /// up with the true traversal window; the solver's cost picks the survivor.
    /// </summary>
    public static class GapPrimitives
    {
        /// <summary>
        /// Writes <paramref name="variants"/> control sequences of length
        /// <paramref name="horizon"/> into <paramref name="buffer"/> starting at
        /// <paramref name="offset"/> (flattened). Returns the number written.
        /// </summary>
        public static int Synthesize(in DetectedGap gap, in State state, in Dynamics dyn,
            float dt, int horizon, int variants, Control[] buffer, int offset)
        {
            if (variants <= 0 || horizon <= 0) return 0;

            var headingErr = Cost.WrapRadians(gap.axisAngle - state.yaw);
            var torqueSign = headingErr >= 0f ? 1f : -1f;
            var alignSteps = dyn.maxYawRate > 0f
                ? math.clamp((int)math.ceil(math.abs(headingErr) / (dyn.maxYawRate * dt)), 0, horizon - 1)
                : 0;

            // When does the ship reach the gap? Use the speed component toward the gap,
            // floored so a slow ship still schedules the pulse inside the horizon.
            var axisDir = new float2(-math.sin(gap.axisAngle), math.cos(gap.axisAngle));
            var speedAlong = math.max(math.dot(state.vel, axisDir), 0.25f * dyn.maxSpeed);
            var entryStep = math.clamp((int)math.round(gap.depth / speedAlong / dt),
                alignSteps, horizon - 1);

            var written = 0;
            for (var v = 0; v < variants; v++)
            {
                var sign = (v & 1) == 0 ? 1f : -1f;
                var pulseLen = 2 + (v >> 1); // 2,2,3,3,4,4,...
                var pulseStart = math.max(entryStep - pulseLen / 2, 0);
                var pulseEnd = pulseStart + pulseLen;         // exclusive
                var unwindEnd = pulseEnd + pulseLen;          // exclusive

                var seq = offset + written * horizon;
                for (var j = 0; j < horizon; j++)
                {
                    var strafe = 0f;
                    if (j >= pulseStart && j < pulseEnd) strafe = sign;
                    else if (j >= pulseEnd && j < unwindEnd) strafe = -sign;

                    buffer[seq + j] = new Control
                    {
                        thrust = 1f,
                        strafe = strafe,
                        yawTorque = j < alignSteps ? torqueSign : 0f,
                        boost = 0f,
                    };
                }
                written++;
            }
            return written;
        }
    }
}
