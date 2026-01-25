using System.Collections;
using Game;
using UnityEngine;
using Utils;

namespace Ships
{
public class Spawner
{
    private readonly ShipSpawnerSettings settings;
    
    private Camera cacheMainCamera;
    private Camera LazyCacheCamera => cacheMainCamera ??= Camera.main;

    public Spawner(ShipSpawnerSettings settings, SubscribedSet<Ship> activeShips)
    {
        this.settings = settings;
        activeShips.OnAdd += (s => s.Damage.OnDeath += OnShipDeath);
        activeShips.OnRemove += (s => s.Damage.OnDeath -= OnShipDeath);
    }
    
    private void OnShipDeath(Ship deadShip, Ship killer)
    { 
        GameContext.Instance.StartCoroutine(WaitAndRespawnShip(settings.enemyRespawnDelay, deadShip));
    }

    private IEnumerator WaitAndRespawnShip(float delay, Ship respawnShip)
    {
        yield return new WaitForSeconds(delay);
        RespawnShipAtRandomPos(respawnShip);
    }

    private void RespawnShipAtRandomPos(Ship respawnShip)
    {
        respawnShip.transform.position = GetRandomOffscreenPosition();
        respawnShip.ResetShip();
    }

    private Vector3 GetRandomOffscreenPosition()
    {
        var pos = Random.insideUnitSphere.normalized * settings.offscreenDistance + LazyCacheCamera.transform.position;
        return GamePlane.ProjectOntoPlane(pos);
    }
}
}
