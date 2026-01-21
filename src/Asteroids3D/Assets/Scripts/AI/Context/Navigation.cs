using AI.Computers;
using UnityEngine;

namespace AI.Context
{
    public class Navigation
    {
        private readonly ShipInfo shipInfo;
        private readonly Scout scout;
        private readonly Navigator navigator;

        public Navigation(ShipInfo shipInfo, Scout scout, Navigator navigator)
        {
            this.shipInfo = shipInfo;
            this.scout = scout;
            this.navigator = navigator;
        }

        public Vector2 VectorToWaypoint => navigator?.CurrentWaypoint.isValid == true
            ? navigator.CurrentWaypoint.position - shipInfo.Pos
            : Vector2.zero;

        public bool NearAsteroidCover => scout.HasNearbyCover;
    }
}
