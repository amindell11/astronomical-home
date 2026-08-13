using Unity.Mathematics;

namespace Movement.MPC
{
    // Objective term: where the ship is going. The reference is either the decision's world-plane
    // velocity or its VEL slot's polar command re-resolved against the predicted referent each step.
    public static partial class Cost
    {
        /// <summary>Squared velocity-tracking error normalized by maxSpeed²; 0 at the reference. World-plane throughout — State.vel and the reference share the frame, so no conversion.</summary>
        internal static float VelocityTrackCost(float2 vel, float2 velocityReference, float maxSpeedSq) =>
            maxSpeedSq > 0f ? math.lengthsq(vel - velocityReference) / maxSpeedSq : 0f;

        /// <summary>World velocity reference for the VEL slot's polar command, relative to the referent's motion so signs keep their meaning against a moving referent (vr > 0 closes, vt > 0 CCW). Below ε range the polar directions are undefined and the reference is a pure velocity match. Deliberately unclamped: an unreachable reference stays the honest command.</summary>
        internal static float2 AnchoredVelocityRef(float2 shipPos, float2 refPos, float2 refVel,
            float radialSpeed, float tangentialSpeed)
        {
            var toRef = refPos - shipPos;
            var dist = math.length(toRef);
            if (dist < 1e-4f) return refVel;
            var losHat = toRef / dist;
            var tangentHat = new float2(losHat.y, -losHat.x);
            return refVel + radialSpeed * losHat + tangentialSpeed * tangentHat;
        }
    }
}
