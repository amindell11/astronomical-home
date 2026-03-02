using System;
using System.Collections;
using Game;
using UnityEngine;

namespace Ships
{
    public class Spawner
    {
        private readonly ShipSpawnerSettings settings;
        private readonly Func<Transform> worldCenterProvider;

        public Spawner(ShipSpawnerSettings settings, Func<Transform> worldCenterProvider)
        {
            this.settings = settings;
            this.worldCenterProvider = worldCenterProvider;
        }

        public IEnumerator WaitAndRespawnShip(float delay, Ship respawnShip)
        {
            yield return new WaitForSeconds(delay);
            RespawnShipAtRandomPos(respawnShip);
        }

        public void RespawnShipAtRandomPos(Ship respawnShip)
        {
            respawnShip.transform.position = GetRandomOffscreenPosition();
            respawnShip.ResetShip();
        }

        public Vector3 GetRandomOffscreenPosition()
        {
            var worldCenter = worldCenterProvider?.Invoke();
            var centerPos = worldCenter ? worldCenter.position : Vector3.zero;
            var pos = UnityEngine.Random.insideUnitSphere.normalized * settings.offscreenDistance + centerPos;
            return GamePlane.ProjectOntoPlane(pos) + GamePlane.Origin;
        }
    }
}
