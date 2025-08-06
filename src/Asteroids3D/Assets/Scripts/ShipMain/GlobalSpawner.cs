using UnityEngine;
using System.Collections;
using Utils;
namespace ShipMain
{
public class GlobalSpawner : MonoBehaviour
{
    private readonly MainGameContext mainGameContext = MainGameContext.Singleton;

    [Header("Game Flow Settings")]
    [SerializeField] private float restartDelay = 3f;

    [SerializeField] private bool restartOnPlayerDeath = true;

    [Header("Enemy Respawn Settings")]
    [SerializeField] private float enemyRespawnDelay = 3f;

    [SerializeField] private float offscreenDistance = 25f;
    private Camera cacheMainCamera;
    private SubscribedSet<Ship> subscribedShips;
    private Camera LazyCacheCamera => cacheMainCamera ??= Camera.main;
    private void Awake()
    {
        subscribedShips = new SubscribedSet<Ship>(
            onAdd: ship => ship.DamageHandler.OnDeath += OnShipDeath,
            onRemove: ship => ship.DamageHandler.OnDeath -= OnShipDeath
        );
    }
    private void OnShipDeath(Ship deadShip, Ship killer)
    {
        if (!deadShip || mainGameContext.CurrentState == GameState.GameOver) return;
        bool isPlayer = deadShip.CompareTag(TagNames.Player);
        if (isPlayer && restartOnPlayerDeath)
            mainGameContext.RestartGame();
        else 
            mainGameContext.StartCoroutine(WaitAndRespawnShip(enemyRespawnDelay, deadShip));
        
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