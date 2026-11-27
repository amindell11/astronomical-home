using System;
using AI.Tactics;
using UnityEngine;

namespace AI.Maneuvers
{
    internal sealed class BreakOff : Maneuver
    {
        private readonly Func<Vector2, Vector2, float, Vector2> flee;

        internal BreakOff(Func<Vector2, Vector2, float, Vector2> flee) => this.flee = flee;

        internal override ManeuverStep Fly(in ManeuverFrame frame) =>
            new ManeuverStep(flee(frame.self.pos, frame.them.pos, frame.speed), engages: false,
                keepBorder: frame.temper.wounded != WoundedResponse.JumpOut,
                jump: frame.wounded && frame.temper.wounded == WoundedResponse.JumpOut
                    && Vector2.Distance(frame.self.pos, frame.them.pos) > frame.temper.jumpsBeyond);
    }
}
