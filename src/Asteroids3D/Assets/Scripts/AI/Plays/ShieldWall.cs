using System;
using AI.Pilots;
using Movement;
using UnityEngine;

namespace AI.Plays
{
    [CreateAssetMenu(fileName = "ShieldWall", menuName = "AI/Plays/Shield Wall")]
    public sealed class ShieldWall : WavePlay
    {
        [Tooltip("Units between ships on the wall.")]
        [Min(0f)] public float spacing = 8f;

        [Tooltip("Degrees a second the wall swings toward the target while the wave imposes.")]
        [Min(0f)] public float swingRate = 20f;

        [Tooltip("Degrees the wall swings toward the target while the wave imposes, at most.")]
        [Min(0f)] public float maxSwing = 50f;

        [Tooltip("Degrees a second the door swings shut when the target goes for the free end.")]
        [Min(0f)] public float slamRate = 70f;

        [Tooltip("Degrees the door swings shut, at most.")]
        [Min(0f)] public float maxSlam = 110f;

        [Tooltip("Units from either end of the wall at which it reads which end the target goes for.")]
        [Min(0f)] public float readRange = 14f;

        [Tooltip("Seconds the answer holds before the wall re-forms.")]
        [Min(0f)] public float answerSeconds = 3f;

        [Tooltip("Seconds each ship's turn to fire lasts.")]
        [Min(0f)] public float rippleSlot = 0.4f;

        internal override float AnswerSeconds => answerSeconds;

        internal override PlayRead Read(in PlayView view)
        {
            if (view.minions == 0) return PlayRead.None;
            var free = Slot(view, view.minions);
            var hinge = view.rival.pos;
            var atFree = Vector2.Distance(view.target.pos, free);
            var atHinge = Vector2.Distance(view.target.pos, hinge);
            if (atFree > readRange && atHinge > readRange) return PlayRead.None;
            return atFree < atHinge ? PlayRead.A : PlayRead.B;
        }

        internal override PlayOrder MinionOrder(in PlayView view, in Kinematics self, int rank, float speed)
        {
            // A dropped play searches; the wall angle is only for a live impose.
            if (view.stage == PlayStage.Searching)
            {
                var rival = view.rival;
                return SearchOrder(rival, self, rank, speed);
            }

            var velocity = Vector2.ClampMagnitude(view.rival.vel + (Slot(view, rank) - self.pos), speed);
            var engages = view.stage != PlayStage.Reforming
                && (view.stage == PlayStage.AnswerA || view.stage == PlayStage.AnswerB
                    || Mathf.FloorToInt(view.imposedSeconds / rippleSlot) % Mathf.Max(view.minions, 1) == rank - 1);
            return new PlayOrder(velocity, engages);
        }

        internal override PlayOrder? RivalOrder(in PlayView view, float speed)
        {
            if (view.stage != PlayStage.AnswerB) return null;
            var rival = view.rival;
            var target = view.target;
            return new PlayOrder(RangerBrain.HoldRangeVelocity(rival, target, 0f, speed), engages: true);
        }

        private Vector2 Slot(in PlayView view, int rank)
        {
            var toTarget = (view.target.pos - view.rival.pos).normalized;
            var axis = Vector2.Perpendicular(toTarget) * view.mirror;
            var radians = Angle(view) * Mathf.Deg2Rad;
            var dir = axis * Mathf.Cos(radians) + toTarget * Mathf.Sin(radians);
            return view.rival.pos + dir * rank * spacing;
        }

        private float Angle(in PlayView view)
        {
            var swung = Mathf.Min(maxSwing, swingRate * view.imposedSeconds);
            float Shut(float seconds) => Mathf.Min(maxSlam, swung + slamRate * seconds);
            switch (view.stage)
            {
                case PlayStage.Imposing:
                case PlayStage.Committing:
                    return swung;
                case PlayStage.AnswerA:
                    return Shut(view.stageSeconds);
                case PlayStage.AnswerB:
                    return swung;
                case PlayStage.Reforming:
                    if (view.read != PlayRead.A && view.read != PlayRead.B)
                        throw new InvalidOperationException("A re-forming wall has no answer to come back from.");
                    var from = view.read == PlayRead.A ? Shut(view.answerSeconds) : swung;
                    if (view.stageSeconds >= reformSeconds) return 0f;
                    return Mathf.Lerp(from, 0f, view.stageSeconds / reformSeconds);
                default:
                    throw new ArgumentOutOfRangeException(nameof(view.stage), view.stage, null);
            }
        }
    }
}
