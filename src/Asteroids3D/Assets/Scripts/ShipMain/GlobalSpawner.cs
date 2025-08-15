using UnityEngine;
using System.Collections;
using Game;
using Utils;
namespace ShipMain
{
public class GlobalSpawner
{
    [Header("Game Flow Settings")]
    [SerializeField] private float restartDelay = 3f;

    [SerializeField] private bool restartOnPlayerDeath = false;

    [Header("Enemy Respawn Settings")]
    [SerializeField] private float enemyRespawnDelay = 3f;

    [SerializeField] private float offscreenDistance = 25f;
    private Camera cacheMainCamera;
    public SubscribedSet<Ship> SubscribedShips { get; private set; }
    private Camera LazyCacheCamera => cacheMainCamera ??= Camera.main;

    public GlobalSpawner(params Ship[] ships)
    {
        SubscribedShips = new SubscribedSet<Ship>(
            onAdd: ship => ship.DamageHandler.OnDeath += OnShipDeath,
            onRemove: ship => ship.DamageHandler.OnDeath -= OnShipDeath
        );
        SubscribedShips.AddAll(ships);
    }
    
    private void OnShipDeath(Ship deadShip, Ship killer)
    {
        var game = MainGameContext.Singleton;
        if (game.CurrentState is GameState.GameOver) return;
        bool isPlayer =  deadShip && deadShip.CompareTag(TagNames.Player);
        if (isPlayer && restartOnPlayerDeath)
            game.RestartGame();
        else 
            game.StartCoroutine(WaitAndRespawnShip(enemyRespawnDelay, deadShip));
        
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
        var pos = Random.insideUnitSphere.normalized * offscreenDistance + LazyCacheCamera.transform.position;
        return GamePlane.ProjectOntoPlane(pos);
    }
}
}