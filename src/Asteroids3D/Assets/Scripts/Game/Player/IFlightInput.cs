using UnityEngine;

namespace Game.Player
{
    public interface IFlightInput
    {
        float Thrust { get; }
        float Strafe { get; }
        bool PrimaryFire { get; }
        bool SecondaryFire { get; }
        bool BoostDown { get; }
        Vector2 ScreenAimPoint { get; }
    }
}
