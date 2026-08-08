using Unity.Mathematics;

namespace Movement.MPC
{
    // Objective term: where the ship is going. The reference is either the decision's world-plane
    // velocity or its enemy-polar command re-resolved against the predicted anchor each step.
    public static partial class Cost
    {
        /// <summary>Squared velocity-tracking error normalized by maxSpeed²; 0 at the reference. World-plane throughout — State.vel and the reference share the frame, so no conversion.</summary>
        internal static float VelocityTrackCost(float2 vel, float2 velocityReference, float maxSpeedSq) =>
            maxSpeedSq > 0f ? math.lengthsq(vel - velocityReference) / maxSpeedSq : 0f;

        /// <summary>World velocity reference for the anchored polar command, relative to the enemy's motion so signs keep their meaning against a moving anchor (vr > 0 closes, vt > 0 CCW). Below ε range the polar directions are undefined and the reference is a pure velocity match. Deliberately unclamped: an unreachable reference stays the honest command.</summary>
        internal static float2 AnchoredVelocityRef(float2 shipPos, float2 enemyPos, float2 enemyVel, in AnchoredIntent anchored)
        {
            var toEnemy = enemyPos - shipPos;
            var dist = math.length(toEnemy);
            if (dist < 1e-4f) return enemyVel;
            var losHat = toEnemy / dist;
            var tangentHat = new float2(losHat.y, -losHat.x);
            return enemyVel + anchored.radialSpeed * losHat + anchored.tangentialSpeed * tangentHat;
        }
    }
}
