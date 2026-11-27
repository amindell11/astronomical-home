using AI.Tactics;
using Movement;

namespace AI.Maneuvers
{
    internal readonly struct ManeuverFrame
    {
        public readonly Kinematics self;
        public readonly Kinematics them;
        public readonly Situation situation;
        public readonly TacticsSettings temper;
        public readonly float speed;
        public readonly float maxSpeed;
        public readonly float speedFraction;
        public readonly float gunRange;
        public readonly int flankSide;
        public readonly int place;
        public readonly bool wounded;

        internal ManeuverFrame(Kinematics self, Kinematics them, Situation situation, TacticsSettings temper,
            float speed, float maxSpeed, float speedFraction, float gunRange, int flankSide, int place, bool wounded)
        {
            this.self = self;
            this.them = them;
            this.situation = situation;
            this.temper = temper;
            this.speed = speed;
            this.maxSpeed = maxSpeed;
            this.speedFraction = speedFraction;
            this.gunRange = gunRange;
            this.flankSide = flankSide;
            this.place = place;
            this.wounded = wounded;
        }
    }
}
