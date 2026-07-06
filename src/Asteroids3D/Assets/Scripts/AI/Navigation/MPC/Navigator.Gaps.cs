using AI.Scanning;
using Movement;
using Unity.Mathematics;
using UnityEngine;

namespace Movement.MPC
{
    /// <summary>
    /// Gap-threading layer: each tick the navigator detects free corridors between obstacles,
    /// picks one with hysteresis, and synthesizes scripted "thread the gap" primitives that get
    /// injected into the MPC's CEM candidate set. Also counts tight-gap transits for telemetry.
    /// Pure runtime helpers composed by <see cref="ComputeCommand"/>.
    /// </summary>
    public partial class Navigator
    {
        private GapDetector gapDetector;
        private GapSelector gapSelector;
        private Gap[] gapBuffer;
        private int gapBufferCount;
        private Control[] primitiveBuffer;
        private int primitiveCount;
        private bool bankGapActive;

        /// <summary>Count of tight (bank-only) gaps this navigator has transited. Reset on Initialize.</summary>
        public int GapsThreaded { get; private set; }

        // Editor/telemetry read-only views.
        internal Gap[] GapBuffer => gapBuffer;
        internal int GapCount => gapBufferCount;
        internal bool HasChosenGap => gapSelector != null && gapSelector.HasChosen;
        internal Gap ChosenGap => gapSelector != null ? gapSelector.Chosen : default;
        internal Control[] PrimitiveBuffer => primitiveBuffer;
        internal int PrimitiveCount => primitiveCount;

        private void InitGaps()
        {
            gapDetector = new GapDetector();
            gapSelector = new GapSelector();
            gapBuffer = new Gap[3];
            primitiveBuffer = new Control[GapPrimitives.MaxVariants * math.max(1, mpcSettings.Horizon)];
            gapBufferCount = 0;
            primitiveCount = 0;
            bankGapActive = false;
            GapsThreaded = 0;
        }

        /// <summary>
        /// Detects gaps toward the goal, selects one (hysteresis), and synthesizes threading
        /// primitives into <see cref="primitiveBuffer"/>. Returns the primitive count (0 = none).
        /// </summary>
        private int ComputeGapPrimitives(in Kinematics kin, in ObstacleScan scan)
        {
            gapBufferCount = 0;
            primitiveCount = 0;

            if (gapDetector == null || !mpcSettings.enableGapInjection || !enableObstacleAvoidance || scan.count == 0)
            {
                gapSelector?.Reset();
                return 0;
            }

            var pos = new float2(kin.pos.x, kin.pos.y);
            var toGoal = GoalPos() - pos;
            var goalDist = math.length(toGoal);
            if (goalDist < 1e-2f)
            {
                gapSelector.Reset();
                return 0;
            }

            var goalDir = toGoal / goalDist;
            var workingRange = math.max(10f, mpcSettings.horizonSeconds * shipDynamics.maxSpeed);
            gapBufferCount = gapDetector.Detect(pos, goalDir, scan,
                shipDynamics.shipRadius, shipDynamics.maxBankAngleRad, mpcSettings.obstacleSafetyMargin,
                workingRange, gapBuffer, gapBuffer.Length);

            if (!gapSelector.Select(gapBuffer, gapBufferCount, mpcSettings.gapHysteresisMargin, out var chosen))
                return 0;

            var horizon = mpcSettings.Horizon;
            var needed = GapPrimitives.MaxVariants * horizon;
            if (primitiveBuffer.Length != needed)
                primitiveBuffer = new Control[needed];

            var cfg = mpcSettings.ToConfig();
            Mpc.ApplyDynamicsTo(ref cfg, shipDynamics);
            var initial = new State
            {
                pos = pos,
                vel = new float2(kin.vel.x, kin.vel.y),
                yaw = kin.yaw * Mathf.Deg2Rad,
                yawRate = kin.yawRate * Mathf.Deg2Rad,
            };

            primitiveCount = GapPrimitives.Synthesize(initial, chosen, cfg, shipDynamics,
                primitiveBuffer, GapPrimitives.MaxVariants, horizon);
            return primitiveCount;
        }

        // Counts each tight (bank-only) gap once, when the ship reaches its mouth.
        private void UpdateGapTelemetry(in Kinematics kin)
        {
            if (gapSelector == null || !gapSelector.HasChosen)
            {
                bankGapActive = false;
                return;
            }

            var chosen = gapSelector.Chosen;
            var atMouth = chosen.classification == GapClass.BankOnly &&
                          chosen.mouthDist < 2f * shipDynamics.shipRadius;

            if (atMouth)
            {
                if (!bankGapActive)
                {
                    GapsThreaded++;
                    bankGapActive = true;
                }
            }
            else if (chosen.mouthDist > 3f * shipDynamics.shipRadius)
            {
                bankGapActive = false;
            }
        }
    }
}
