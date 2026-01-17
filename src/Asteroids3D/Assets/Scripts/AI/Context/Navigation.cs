using AI.Computers;
using UnityEngine;

namespace AI.Context
{
    public class Navigation
    {
        private readonly ShipInfo shipInfo;
        private readonly Sensors sensors;
        private readonly Navigator navigator;

        public Navigation(ShipInfo shipInfo, Sensors sensors, Navigator navigator)
        {
            this.shipInfo = shipInfo;
            this.sensors = sensors;
            this.navigator = navigator;
        }

        public Vector2 VectorToWaypoint => navigator?.CurrentWaypoint.isValid == true
            ? navigator.CurrentWaypoint.position - shipInfo.Pos
            : Vector2.zero;

        public bool NearAsteroidCover => sensors.HasNearbyCover(shipInfo.Pos3D);
    }
}
