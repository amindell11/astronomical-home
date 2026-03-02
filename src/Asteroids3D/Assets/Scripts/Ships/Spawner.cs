using System.Collections;
using Game;
using UnityEngine;
using Utils;

namespace Ships
{
public class Spawner
{
    private readonly ShipSpawnerSettings settings;
    private readonly IShipRegistry registry;
    private Transform worldCenter;

    public Spawner(ShipSpawnerSettings settings, SubscribedSet<Ship> activeShips, IShipRegistry registry)
    {
        this.settings = settings;
        this.registry = registry;
        activeShips.OnAdd += (s => s.Damage.OnDeath += OnShipDeath);
        activeShips.OnRemove += (s => s.Damage.OnDeath -= OnShipDeath);
    }
    
    private void OnShipDeath(ShipId deadShipId, ShipId _killerId)
    {
        if (!registry.TryGetShip(deadShipId, out var deadShip)) return;
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
        worldCenter ??= GameContext.Instance.WorldFollow?.transform;
        var centerPos = worldCenter ? worldCenter.position : Vector3.zero;
        var pos = Random.insideUnitSphere.normalized * settings.offscreenDistance + centerPos;
        return GamePlane.WorldPointToPlane(pos);
    }
}
}
