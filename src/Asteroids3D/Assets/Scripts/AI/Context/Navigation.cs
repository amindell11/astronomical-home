using UnityEngine;

namespace AI.Context
{
    public class Navigation
    {
        private readonly ShipInfo shipInfo;
        private readonly Navigator navigator;

        public Navigation(ShipInfo shipInfo, Navigator navigator)
        {
            this.shipInfo = shipInfo;
            this.navigator = navigator;
        }

        public Vector2 VectorToWaypoint => navigator?.CurrentWaypoint.isValid == true
            ? navigator.CurrentWaypoint.position - shipInfo.Pos
            : Vector2.zero;
    }
}
