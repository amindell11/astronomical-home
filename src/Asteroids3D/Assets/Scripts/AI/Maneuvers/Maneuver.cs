namespace AI.Maneuvers
{
    /// <summary>
    /// One tactic's flight law. The brain flies exactly one maneuver per recompute.
    /// </summary>
    internal abstract class Maneuver
    {
        internal abstract ManeuverStep Fly(in ManeuverFrame frame);
    }
}
