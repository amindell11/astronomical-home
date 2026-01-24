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
    public SubscribedSet<Ship> SubscribedShips { get; private set; }
    private Camera LazyCacheCamera => cacheMainCamera ??= Camera.main;

    public Spawner(ShipSpawnerSettings settings, params Ship[] ships)
    {
        this.settings = settings;
        SubscribedShips = new SubscribedSet<Ship>(
            add: ship => ship.Damage.OnDeath += OnShipDeath,
            remove: ship => ship.Damage.OnDeath -= OnShipDeath
        );
        SubscribedShips.AddAll(ships);
    }
    
    private void OnShipDeath(Ship deadShip, Ship killer)
    {
        var game = GameContext.Singleton;
        if (game.CurrentState is GameState.GameOver) return;
        var isPlayer =  deadShip && deadShip.CompareTag(TagNames.Player);
        if(!settings) return;
        if (isPlayer && settings.restartOnPlayerDeath)
            game.RestartGame();
        else 
            game.StartCoroutine(WaitAndRespawnShip(settings.enemyRespawnDelay, deadShip));
        
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
