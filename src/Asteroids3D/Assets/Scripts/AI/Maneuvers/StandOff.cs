using AI.Pilots;

namespace AI.Maneuvers
{
    internal sealed class StandOff : Maneuver
    {
        internal override ManeuverStep Fly(in ManeuverFrame frame) =>
            new ManeuverStep(RangerBrain.HoldRangeVelocity(frame.self, frame.them,
                frame.situation.targetRange + frame.temper.standOffMargin, frame.speed),
                engages: !frame.situation.inTargetsCone && frame.situation.alliesInLane == 0);
    }
}
