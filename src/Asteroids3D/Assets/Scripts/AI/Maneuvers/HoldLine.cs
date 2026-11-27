using UnityEngine;

namespace AI.Maneuvers
{
    internal sealed class HoldLine : Maneuver
    {
        internal override ManeuverStep Fly(in ManeuverFrame frame)
        {
            var self = frame.self;
            var leader = frame.situation.leader;
            var them = frame.them;
            var point = leader.pos - Vector2.Perpendicular((them.pos - leader.pos).normalized)
                * frame.place * frame.temper.lineSpacing;
            return new ManeuverStep(Vector2.ClampMagnitude(leader.vel + (point - self.pos), frame.speed),
                engages: frame.situation.alliesInLane == 0);
        }
    }
}
