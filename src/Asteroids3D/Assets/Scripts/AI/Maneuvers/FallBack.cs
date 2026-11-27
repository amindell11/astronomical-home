using AI.Pilots;
using UnityEngine;

namespace AI.Maneuvers
{
    internal sealed class FallBack : Maneuver
    {
        internal override ManeuverStep Fly(in ManeuverFrame frame)
        {
            var fallBackRange = Mathf.Max(frame.situation.myRange - frame.temper.fallBackMargin,
                frame.gunRange + frame.temper.attackRunMargin);
            return new ManeuverStep(RangerBrain.HoldRangeVelocity(frame.self, frame.them, fallBackRange, frame.speed),
                engages: frame.situation.alliesInLane == 0);
        }
    }
}
