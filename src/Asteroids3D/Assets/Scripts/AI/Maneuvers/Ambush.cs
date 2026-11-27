using AI.Pilots;

namespace AI.Maneuvers
{
    internal sealed class Ambush : Maneuver
    {
        internal override ManeuverStep Fly(in ManeuverFrame frame)
        {
            var self = frame.self;
            var them = frame.them;
            var situation = frame.situation;
            if (situation.selfConcealed || situation.kitReady)
            {
                var point = them.pos + them.Forward * frame.temper.ambushAhead;
                return new ManeuverStep(ArchetypeRoam.SeekVelocity(self.pos, point, frame.speed), engages: false,
                    boost: situation.kitReady && !situation.selfConcealed);
            }

            return new ManeuverStep(RangerBrain.HoldRangeVelocity(in self, them, frame.gunRange, frame.speed),
                engages: true);
        }
    }
}
