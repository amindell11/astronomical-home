using AI.Pilots;

namespace AI.Maneuvers
{
    internal sealed class Flank : Maneuver
    {
        internal override ManeuverStep Fly(in ManeuverFrame frame) =>
            new ManeuverStep(ArchetypeLaws.OrbitVelocity(frame.self, frame.them, frame.gunRange, frame.flankSide,
                frame.speedFraction, frame.maxSpeed), engages: frame.situation.alliesInLane == 0);
    }
}
