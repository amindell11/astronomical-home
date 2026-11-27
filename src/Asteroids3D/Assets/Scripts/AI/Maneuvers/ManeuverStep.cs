using UnityEngine;

namespace AI.Maneuvers
{
    internal readonly struct ManeuverStep
    {
        public readonly Vector2 velocity;
        public readonly bool engages;
        public readonly bool keepBorder;
        public readonly bool jump;
        public readonly bool boost;

        internal ManeuverStep(Vector2 velocity, bool engages, bool keepBorder = true, bool jump = false,
            bool boost = false)
        {
            this.velocity = velocity;
            this.engages = engages;
            this.keepBorder = keepBorder;
            this.jump = jump;
            this.boost = boost;
        }
    }
}
