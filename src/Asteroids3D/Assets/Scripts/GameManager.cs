using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public enum GameState
{
    Playing,
    GameOver
}

public class GameManager : BaseGameContext
{
    public static GameManager Instance { get; private set; }

    [Header("Game Flow Settings")]
    [SerializeField] private float restartDelay = 3f; // seconds before restarting after player death
    [SerializeField] private bool restartOnPlayerDeath = true;
    [Header("Enemy Respawn Settings")]
    [SerializeField] private float enemyRespawnDelay = 3f;
    [SerializeField] private float offscreenDistance = 25f;

    private GameState currentState = GameState.Playing;
    public GameState CurrentState => currentState;
    
    // Track enemy ships for respawning
    private Camera mainCamera;

    // Optimization: Cache WaitForSeconds to avoid allocations
    private WaitForSeconds cachedEnemyRespawnWait;

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

        // Cache WaitForSeconds
        cachedEnemyRespawnWait = new WaitForSeconds(enemyRespawnDelay);

        // Register for OnDeath events on all current ships (objects in the "Ship" layer)
        RegisterAllShipHandlers();
    }

    protected override void Start()
    {
        base.Start();
        currentState = GameState.Playing;        
        //Shader.WarmupAllShaders();
    }

    /// <summary>
    /// Call this when the player's ship has been destroyed.
    /// </summary>
    public void HandlePlayerDeath(Ship playerShip)
    {
        if (currentState == GameState.GameOver) return;
        if (restartOnPlayerDeath)
        {
            currentState = GameState.GameOver;
            RLog.Core("Player ship destroyed. Game Over!");
            Invoke(nameof(RestartGame), restartDelay);
        } else {
            IEnumerator respawnRoutine = WaitAndRespawn(enemyRespawnDelay, playerShip);
            StartCoroutine(respawnRoutine);
        }
    }
    
    /// <summary>
    /// Call this when an enemy ship has been destroyed.
    /// </summary>
    public void HandleEnemyDeath(Ship respawnShip)
    {
        if (currentState != GameState.Playing) return;
        
        RLog.Core("Enemy ship destroyed. Scheduling respawn...");
        IEnumerator respawnRoutine = WaitAndRespawn(enemyRespawnDelay, respawnShip);
        StartCoroutine(respawnRoutine);
    }
    
    private IEnumerator WaitAndRespawn(float delay, Ship respawnShip)
    {
        // Use cached WaitForSeconds if delay matches, otherwise create new one
        if (!Mathf.Approximately(delay, enemyRespawnDelay))
            cachedEnemyRespawnWait = new WaitForSeconds(delay);
        yield return cachedEnemyRespawnWait;
        RespawnRandomEnemy(respawnShip);
    }
    
    private void RespawnRandomEnemy(Ship respawnShip)
    {
        respawnShip.gameObject.SetActive(true);

        // After the object (and its components) are enabled, reset physics & damage
        respawnShip.ResetShip();

        // Find a random offscreen position
        Vector3 respawnPosition = GetRandomOffscreenPosition();
        respawnShip.transform.position = respawnPosition;
        RLog.Core($"Enemy ship respawned at position: {respawnPosition}");
    }
    
    private Vector3 GetRandomOffscreenPosition()
    {
        // Ensure we have a valid mainCamera reference (it may have been destroyed during a scene reload)
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                RLog.CoreWarning("GameManager: No main camera found. Returning Vector3.zero for offscreen position.");
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
        RLog.Core("Restarting game...");
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
        currentState = GameState.Playing;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
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
            ship.OnDeath -= OnShipDeath;
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

        // Find all ships in the scene, optionally filtering by layer named "Ship" if it exists
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
                ship.OnDeath += OnShipDeath;
                subscribedShips.Add(ship);
            }
        }

        // Mark ships cache as dirty so BaseGameContext will refresh on next access
        MarkShipsCacheDirty();
    }

    private void OnShipDeath(Ship deadShip, Ship killer)
    {
        if (deadShip == null) return;

        // Determine if this is the player by tag or team.
        if (deadShip.CompareTag(TagNames.Player))
        {
            HandlePlayerDeath(deadShip);
        }
        else
        {
            HandleEnemyDeath(deadShip);
        }
    }

    // -----------------------------------------------------------------
    // BaseGameContext implementation

    /// <summary>
    /// The logical center of the current play area. For a typical single-scene game this is the world origin.
    /// </summary>
    public override Vector3 CenterPosition => Vector3.zero;

    /// <summary>
    /// Approximate radius of the active play area. Uses <see cref="offscreenDistance"/> which is also the spawn radius.
    /// </summary>
    public override float AreaSize => offscreenDistance;

    /// <summary>
    /// Returns true while the game is in a playing state.
    /// </summary>
    public override bool IsActive => currentState == GameState.Playing;

    // Uses default implementation from BaseGameContext that finds all ships in scene
} 