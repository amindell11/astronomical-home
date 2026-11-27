using UnityEngine;

namespace Game.Player
{
    public interface IFlightInput
    {
        float Thrust { get; }
        float Strafe { get; }
        Vector2 AimScreenPoint { get; }
        bool BoostDown { get; }
        bool PrimaryFire { get; }
        bool SecondaryFire { get; }
    }
}
