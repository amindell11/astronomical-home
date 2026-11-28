using Movement;
using UnityEngine;

namespace AI.Plays
{
    /// <summary>
    /// A formation a wave's rival commands. It reads the player once, then commits to one of two answers.
    /// </summary>
    public abstract class WavePlay : ScriptableObject
    {
        [Tooltip("Seconds between the wave reading the player and committing to its answer.")]
        [Min(0f)] public float readDelay = 0.5f;

        [Tooltip("Seconds the wave takes to re-form between an answer and the next impose.")]
        [Min(0f)] public float reformSeconds = 2.5f;

        [Tooltip("Units between a searching wave's ships, astern of the rival.")]
        [Min(0f)] public float searchSpacing = 8f;

        internal abstract float AnswerSeconds { get; }

        internal abstract PlayRead Read(in PlayView view);

        internal abstract PlayOrder MinionOrder(in PlayView view, in Kinematics self, int rank, float speed);

        // Null leaves the rival on its own tactics.
        internal abstract PlayOrder? RivalOrder(in PlayView view, float speed);

        internal PlayOrder SearchOrder(in Kinematics rival, in Kinematics self, int rank, float speed)
        {
            var point = rival.pos - rival.Forward * rank * searchSpacing;
            return new PlayOrder(Vector2.ClampMagnitude(rival.vel + (point - self.pos), speed), engages: false);
        }
    }
}
