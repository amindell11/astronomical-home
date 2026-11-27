using AI.Pilots;

namespace AI.Maneuvers
{
    internal sealed class Charge : Maneuver
    {
        internal override ManeuverStep Fly(in ManeuverFrame frame) =>
            new ManeuverStep(RangerBrain.HoldRangeVelocity(frame.self, frame.them, frame.temper.chargeRange,
                frame.speed), engages: true);
    }
}
