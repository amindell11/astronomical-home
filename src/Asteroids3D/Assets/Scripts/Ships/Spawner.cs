using System.Collections;
using Game;
using UnityEngine;
using Utils;

namespace Ships
{
public class Spawner
{
    [Header("Game Flow Settings")]
    [SerializeField] private float restartDelay = 3f;

    [SerializeField] private bool restartOnPlayerDeath = false;

    [Header("Enemy Respawn Settings")]
    [SerializeField] private float enemyRespawnDelay = 3f;

    [SerializeField] private float offscreenDistance = 25f;
    
    private Camera cacheMainCamera;
    public SubscribedSet<Ships.Ship> SubscribedShips { get; private set; }
    private Camera LazyCacheCamera => cacheMainCamera ??= Camera.main;

    public Spawner(params Ships.Ship[] ships)
    {
        SubscribedShips = new SubscribedSet<Ships.Ship>(
            add: ship => ship.Damage.OnDeath += OnShipDeath,
            remove: ship => ship.Damage.OnDeath -= OnShipDeath
        );
        SubscribedShips.AddAll(ships);
    }
    
    private void OnShipDeath(Ships.Ship deadShip, Ships.Ship killer)
    {
        var game = Context.Singleton;
        if (game.CurrentState is GameState.GameOver) return;
        var isPlayer =  deadShip && deadShip.CompareTag(TagNames.Player);
        if (isPlayer && restartOnPlayerDeath)
            game.RestartGame();
        else 
            game.StartCoroutine(WaitAndRespawnShip(enemyRespawnDelay, deadShip));
        
    }

    private IEnumerator WaitAndRespawnShip(float delay, Ships.Ship respawnShip)
    {
        yield return new WaitForSeconds(delay);
        RespawnShipAtRandomPos(respawnShip);
    }

    private void RespawnShipAtRandomPos(Ships.Ship respawnShip)
    {
        respawnShip.transform.position = GetRandomOffscreenPosition();
        respawnShip.ResetShip();
    }

    private Vector3 GetRandomOffscreenPosition()
    {
        var pos = Random.insideUnitSphere.normalized * offscreenDistance + LazyCacheCamera.transform.position;
        return GamePlane.ProjectOntoPlane(pos);
    }
}
}
