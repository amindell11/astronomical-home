using Movement;
using UnityEngine;

namespace AI.Plays
{
    public readonly struct PlayView
    {
        public readonly Kinematics rival;
        public readonly Kinematics target;
        public readonly PlayStage stage;
        public readonly float stageSeconds;
        public readonly float imposedSeconds;
        public readonly int mirror;
        public readonly int minions;
        public readonly PlayRead read;
        public readonly float answerSeconds;
        public readonly Vector2 bearing;

        public PlayView(Kinematics rival, Kinematics target, PlayStage stage, float stageSeconds,
            float imposedSeconds, int mirror, int minions, PlayRead read, float answerSeconds, Vector2 bearing)
        {
            this.rival = rival;
            this.target = target;
            this.stage = stage;
            this.stageSeconds = stageSeconds;
            this.imposedSeconds = imposedSeconds;
            this.mirror = mirror;
            this.minions = minions;
            this.read = read;
            this.answerSeconds = answerSeconds;
            this.bearing = bearing;
        }
    }
}
