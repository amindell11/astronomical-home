using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public enum GameState
{
    Playing,
    GameOver
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Flow Settings")]
    [SerializeField] private float restartDelay = 3f; // seconds before restarting after player death
    
    [Header("Enemy Respawn Settings")]
    [SerializeField] private float enemyRespawnDelay = 3f;
    [SerializeField] private float offscreenDistance = 25f;

    private GameState currentState = GameState.Playing;
    public GameState CurrentState => currentState;
    
    // Track enemy ships for respawning
    private Camera mainCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        mainCamera = Camera.main;
        DontDestroyOnLoad(gameObject);

        // Register for OnDeath events on all current ships (objects in the "Ship" layer)
        RegisterAllShipHandlers();
    }

    private void Start()
    {
        currentState = GameState.Playing;        
        //Shader.WarmupAllShaders();
    }

    /// <summary>
    /// Call this when the player's ship has been destroyed.
    /// </summary>
    public void HandlePlayerDeath(ShipMovement playerShip)
    {
        if (currentState == GameState.GameOver) return;

        currentState = GameState.GameOver;
        RLog.Log("Player ship destroyed. Game Over!");
        Invoke(nameof(RestartGame), restartDelay);
    }
    
    /// <summary>
    /// Call this when an enemy ship has been destroyed.
    /// </summary>
    public void HandleEnemyDeath(ShipMovement respawnShip)
    {
        if (currentState != GameState.Playing) return;
        
        RLog.Log("Enemy ship destroyed. Scheduling respawn...");
        IEnumerator respawnRoutine = WaitAndRespawn(enemyRespawnDelay, respawnShip);
        StartCoroutine(respawnRoutine);
    }
    
    private IEnumerator WaitAndRespawn(float delay, ShipMovement respawnShip)
    {
        yield return new WaitForSeconds(delay);
        RespawnRandomEnemy(respawnShip);
    }
    
    private void RespawnRandomEnemy(ShipMovement respawnShip)
    {
        respawnShip.gameObject.SetActive(true);

        // After the object (and its components) are enabled, reset physics & damage
        respawnShip.ResetShip();
        respawnShip.GetComponent<ShipDamageHandler>()?.ResetAll();

        // Find a random offscreen position
        Vector3 respawnPosition = GetRandomOffscreenPosition();
        respawnShip.transform.position = respawnPosition;
        RLog.Log($"Enemy ship respawned at position: {respawnPosition}");
    }
    
    private Vector3 GetRandomOffscreenPosition()
    {
        // Ensure we have a valid mainCamera reference (it may have been destroyed during a scene reload)
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                RLog.LogWarning("GameManager: No main camera found. Returning Vector3.zero for offscreen position.");
                return Vector3.zero;
            }
        }
        Vector3 pos = Random.insideUnitSphere.normalized * offscreenDistance + mainCamera.transform.position;
        pos.y = 0;
        return pos;
    }

    /// <summary>
    /// Reloads the current scene to restart the game.
    /// </summary>
    public void RestartGame()
    {
        RLog.Log("Restarting game...");
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
        currentState = GameState.Playing;
    }

    private void OnEnable()
    {
        // Subscribe to scene loaded callback to refresh references after a scene reload
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // Unsubscribe when this object is disabled/destroyed
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Unsubscribe from ship events we previously registered
        foreach (var ship in subscribedShips)
        {
            if (ship == null) continue;
            var dh = ship.GetComponent<ShipDamageHandler>();
            if (dh != null)
            {
                dh.OnDeath -= OnShipDeath;
            }
        }
        subscribedShips.Clear();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Refresh the main camera reference because the old one was destroyed during scene load
        mainCamera = Camera.main;

        // Scene reload likely created new ship objects – register again
        RegisterAllShipHandlers();
    }

    // -----------------------------------------------------------------
    private readonly List<Ship> subscribedShips = new();

    private void RegisterAllShipHandlers()
    {
        // Clear old list (do NOT unsubscribe here; that happens in OnDisable)
        subscribedShips.RemoveAll(s => s == null);

        // Find all ShipDamageHandlers in the scene, optionally filtering by layer named "Ship" if it exists
        Ship[] ships = FindObjectsByType<Ship>(FindObjectsSortMode.None);
        int shipLayer = LayerMask.NameToLayer("Ship");

        foreach (var ship in ships)
        {
            if (ship == null) continue;

            // If a specific "Ship" layer exists (> -1) and object is not on it, skip
            if (shipLayer != -1 && ship.gameObject.layer != shipLayer)
                continue;

            if (!subscribedShips.Contains(ship))
            {
                ship.damageHandler.OnDeath += OnShipDeath;
                subscribedShips.Add(ship);
            }
        }
    }

    private void OnShipDeath(Ship deadShip)
    {
        if (deadShip == null) return;

        // Determine if this is the player by tag or team.
        if (deadShip.CompareTag("Player"))
        {
            HandlePlayerDeath(deadShip.movement);
        }
        else
        {
            HandleEnemyDeath(deadShip.movement);
        }
    }
} 