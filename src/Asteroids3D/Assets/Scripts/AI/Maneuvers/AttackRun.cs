using AI.Pilots;

namespace AI.Maneuvers
{
    internal sealed class AttackRun : Maneuver
    {
        internal override ManeuverStep Fly(in ManeuverFrame frame) =>
            new ManeuverStep(RangerBrain.HoldRangeVelocity(frame.self, frame.them,
                frame.gunRange + frame.temper.attackRunMargin, frame.speed), engages: true);
    }
}
