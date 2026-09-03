using AI;
using Movement.MPC;
using Unity.MLAgents.Actuators;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One slot's referent capture at the decision boundary: the enemy (choice 0) or a resolved rock slot. A chosen-but-empty rock slot keeps the choice with an unbound rock — the decode zeroes that slot's weight (the referent-invalidation rule applied at decode time).</summary>
    public readonly struct ReferentCapture
    {
        public readonly int choice;        // 0 = enemy, 1..SlotCount = rock slot choice-1
        public readonly AsteroidRef rock;  // bound iff the choice named an occupied slot

        public ReferentCapture(int choice, in AsteroidRef rock)
        {
            this.choice = choice;
            this.rock = rock;
        }

        public bool Enemy => choice == 0;
        public bool Empty => choice != 0 && !rock.IsBound;
    }

    /// <summary>One decision's decoded intent sentence plus the fire/boost branches — all primitives; the MPC-frame assembly happens in <see cref="PolicyBrain"/>.</summary>
    public readonly struct AgentAction
    {
        public readonly float aimOffsetRad;
        public readonly float aimWeight;
        public readonly ReferentCapture aimReferent;

        public readonly float posOffsetR;
        public readonly float posOffsetThetaRad;
        public readonly float posSetpoint;
        public readonly float posWeight;      // Signed
        public readonly ReferentCapture posReferent;
        public readonly ReferentFrame posFrame;

        public readonly float velRadialSpeed;      // m/s, speedRef-scaled unit direction
        public readonly float velTangentialSpeed;  // m/s
        public readonly float velWeight;
        public readonly ReferentCapture velReferent;
        public readonly ReferentFrame velFrame;

        public readonly float laneWeight;  // Signed
        public readonly float fieldWeight;

        public readonly bool firePrimary;
        public readonly bool fireSecondary;
        public readonly bool boost;

        public AgentAction(float aimOffsetRad, float aimWeight, in ReferentCapture aimReferent,
            float posOffsetR, float posOffsetThetaRad, float posSetpoint, float posWeight,
            in ReferentCapture posReferent, ReferentFrame posFrame,
            float velRadialSpeed, float velTangentialSpeed, float velWeight,
            in ReferentCapture velReferent, ReferentFrame velFrame,
            float laneWeight, float fieldWeight, bool firePrimary, bool fireSecondary, bool boost)
        {
            this.aimOffsetRad = aimOffsetRad;
            this.aimWeight = aimWeight;
            this.aimReferent = aimReferent;
            this.posOffsetR = posOffsetR;
            this.posOffsetThetaRad = posOffsetThetaRad;
            this.posSetpoint = posSetpoint;
            this.posWeight = posWeight;
            this.posReferent = posReferent;
            this.posFrame = posFrame;
            this.velRadialSpeed = velRadialSpeed;
            this.velTangentialSpeed = velTangentialSpeed;
            this.velWeight = velWeight;
            this.velReferent = velReferent;
            this.velFrame = velFrame;
            this.laneWeight = laneWeight;
            this.fieldWeight = fieldWeight;
            this.firePrimary = firePrimary;
            this.fireSecondary = fireSecondary;
            this.boost = boost;
        }
    }

    /// <summary>Decodes the sentence action head (#485) — 10 continuous + 8 discrete branches — into <see cref="AgentAction"/>. Direction
    /// heads ride as vectors (angle = the command, magnitude = authority weight; x &gt; 0 is CCW from the
    /// head's zero, the anchored-intent convention), POS rides as an in-frame Cartesian offset plus
    /// setpoint (both arena-radius-normalized) with a signed weight, LANE/FIELD are bare weights. The
    /// decode clamps every channel — training and inference see one range, retiring the documented
    /// unclamped-vs-clipped ONNX mismatch — and resolves referent branches against the roster the policy
    /// just observed (the boundary slot→entity capture). MPC-type-free apart from the carrier enums;
    /// the brain packs the scalars into the anchored nav objective.</summary>
    public static class AgentActions
    {
        public const int ContinuousCount = 10;
        public const int ReferentChoices = 1 + RockSlotRoster.SlotCount;
        public const int FrameChoices = 3;

        // Continuous layout.
        public const int AimX = 0;
        public const int AimY = 1;
        public const int PosX = 2;
        public const int PosY = 3;
        public const int PosSetpoint = 4;
        public const int PosWeight = 5;
        public const int VelRadial = 6;
        public const int VelTangential = 7;
        public const int LaneWeight = 8;
        public const int FieldWeight = 9;

        // Discrete branch layout.
        public const int AimReferentBranch = 0;
        public const int PosReferentBranch = 1;
        public const int VelReferentBranch = 2;
        public const int PosFrameBranch = 3;
        public const int VelFrameBranch = 4;
        public const int FirePrimaryBranch = 5;
        public const int FireSecondaryBranch = 6;
        public const int BoostBranch = 7;

        public static readonly int[] BranchSizes =
        {
            ReferentChoices, ReferentChoices, ReferentChoices, FrameChoices, FrameChoices, 2, 2, 2,
        };

        public static AgentAction Map(in ActionBuffers actions, RockSlotRoster rockSlots,
            float speedRef, float arenaRadius)
        {
            var c = actions.ContinuousActions;
            var d = actions.DiscreteActions;

            var aimReferent = Capture(d[AimReferentBranch], rockSlots);
            var aimX = c[AimX];
            var aimY = c[AimY];
            var aimWeight = Mathf.Clamp01(Mathf.Sqrt(aimX * aimX + aimY * aimY));

            var posReferent = Capture(d[PosReferentBranch], rockSlots);
            var posX = Mathf.Clamp(c[PosX], -1f, 1f);
            var posY = Mathf.Clamp(c[PosY], -1f, 1f);

            var velReferent = Capture(d[VelReferentBranch], rockSlots);
            // vr/vt deliberately unclamped: training ran unclamped, inference clips downstream.
            var velR = c[VelRadial];
            var velT = c[VelTangential];
            var velMagnitude = Mathf.Sqrt(velR * velR + velT * velT);
            var velScale = velMagnitude > 1e-6f ? speedRef / velMagnitude : 0f;

            return new AgentAction(
                Mathf.Atan2(aimX, aimY),
                aimReferent.Empty ? 0f : aimWeight,
                aimReferent,
                Mathf.Sqrt(posX * posX + posY * posY) * arenaRadius,
                Mathf.Atan2(posX, posY),
                Mathf.Clamp01(c[PosSetpoint]) * arenaRadius,
                posReferent.Empty ? 0f : Mathf.Clamp(c[PosWeight], -1f, 1f),
                posReferent,
                (ReferentFrame)d[PosFrameBranch],
                velR * velScale,
                velT * velScale,
                velReferent.Empty ? 0f : Mathf.Clamp01(velMagnitude),
                velReferent,
                (ReferentFrame)d[VelFrameBranch],
                Mathf.Clamp(c[LaneWeight], -1f, 1f),
                Mathf.Clamp01(c[FieldWeight]),
                d[FirePrimaryBranch] == 1,
                d[FireSecondaryBranch] == 1,
                d[BoostBranch] == 1);
        }

        /// <summary>Vocabulary levels for <see cref="WriteMask"/>, decoded from the
        /// EnvParamOverlay.SentenceRelease float by <see cref="VocabularyFromParam"/>. Partial exists
        /// because a single-choice masked branch saturates its softmax and gets zero gradient (the
        /// 2026-08-26 release collapse): two live referent choices keep the branch trainable while
        /// concentrating exploration on the enemy.</summary>
        public enum SentenceVocabulary { Pinned, Partial, Released }

        public static SentenceVocabulary VocabularyFromParam(float sentenceRelease) =>
            sentenceRelease < 0.25f ? SentenceVocabulary.Pinned
            : sentenceRelease < 0.75f ? SentenceVocabulary.Partial
            : SentenceVocabulary.Released;

        /// <summary>The curriculum/vocabulary mask (§Stage C forks 2, 5): pinned = referents→enemy and
        /// frames→Position (the legacy-equivalence point); partial = referents limited to {enemy,
        /// nearest-rock slot}, frames open; released opens every choice whose rock slot is occupied —
        /// an empty slot is never choosable, so the policy binds only what it observed. The secondary
        /// stays disengage-only at every level until marksmanship (#409) arms it — unmasking is a
        /// training-run change, not a schema break.</summary>
        public static void WriteMask(IDiscreteActionMask mask, RockSlotRoster rockSlots, SentenceVocabulary vocabulary)
        {
            for (var choice = 1; choice < ReferentChoices; choice++)
            {
                var open = vocabulary == SentenceVocabulary.Released
                    || (vocabulary == SentenceVocabulary.Partial && choice == 1);
                var enabled = open && rockSlots.TryGetSlot(choice - 1, out _);
                mask.SetActionEnabled(AimReferentBranch, choice, enabled);
                mask.SetActionEnabled(PosReferentBranch, choice, enabled);
                mask.SetActionEnabled(VelReferentBranch, choice, enabled);
            }

            var framesOpen = vocabulary != SentenceVocabulary.Pinned;
            for (var frame = 1; frame < FrameChoices; frame++)
            {
                mask.SetActionEnabled(PosFrameBranch, frame, framesOpen);
                mask.SetActionEnabled(VelFrameBranch, frame, framesOpen);
            }

            mask.SetActionEnabled(FireSecondaryBranch, 1, false);
        }

        private static ReferentCapture Capture(int choice, RockSlotRoster rockSlots)
        {
            if (choice == 0) return new ReferentCapture(0, default);
            rockSlots.TryGetSlot(choice - 1, out var rock);
            return new ReferentCapture(choice, rock);
        }
    }
}
