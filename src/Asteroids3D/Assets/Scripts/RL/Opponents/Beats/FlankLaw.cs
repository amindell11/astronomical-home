using Movement;
using UnityEngine;

namespace RL.Opponents.Beats
{
    /// <summary>The pure flank law (the <see cref="RangerBrain.HoldRangeVelocity"/> style): a point off the target's tail on one side, and a seek onto it that rides the target's own velocity.</summary>
    internal static class FlankLaw
    {
        private const float TailAngleDegrees = 35f;
        private const float Gain = 0.8f;

        internal static Vector2 BlindPoint(in Kinematics enemy, int side, float distance)
        {
            var tail = -enemy.Forward;
            return enemy.pos + distance * (Vector2)(Quaternion.Euler(0f, 0f, side * TailAngleDegrees) * tail);
        }

        internal static Vector2 Velocity(Vector2 selfPos, in Kinematics enemy, Vector2 blindPoint, float speed) =>
            Vector2.ClampMagnitude(enemy.vel + Gain * (blindPoint - selfPos), speed);

        /// <summary>The tail side away from the attackers, else the side this pilot is on.</summary>
        internal static int Side(Vector2 selfPos, in Kinematics enemy, in Crowd crowd)
        {
            var tail = -enemy.Forward;
            var from = crowd.attackers > 0 ? -crowd.offset : selfPos - enemy.pos;
            return tail.x * from.y - tail.y * from.x >= 0f ? 1 : -1;
        }
    }
}
