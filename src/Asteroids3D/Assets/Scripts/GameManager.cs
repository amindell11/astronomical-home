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
    private List<GameObject> enemyShips = new List<GameObject>();
    private Camera mainCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        currentState = GameState.Playing;
        mainCamera = Camera.main;
        
        // Find all enemy ships in the scene
        FindEnemyShips();
        
        //Shader.WarmupAllShaders();
    }
    
    private void FindEnemyShips()
    {
        Ship[] allShips = FindObjectsOfType<Ship>();
        enemyShips.Clear();
        
        foreach (Ship ship in allShips)
        {
            // Check if this is an enemy ship (has AIShipInput component and is not player ship)
            if (ship.GetComponent<AIShipInput>() != null)
            {
                enemyShips.Add(ship.gameObject);
            }
        }
        
        Debug.Log($"Found {enemyShips.Count} enemy ships to manage");
    }

    /// <summary>
    /// Call this when the player's ship has been destroyed.
    /// </summary>
    public void HandlePlayerDeath(Ship playerShip)
    {
        if (currentState == GameState.GameOver) return;

        currentState = GameState.GameOver;
        Debug.Log("Player ship destroyed. Game Over!");
        Invoke(nameof(RestartGame), restartDelay);
    }
    
    /// <summary>
    /// Call this when an enemy ship has been destroyed.
    /// </summary>
    public void HandleEnemyDeath(Ship respawnShip)
    {
        if (currentState != GameState.Playing) return;
        
        Debug.Log("Enemy ship destroyed. Scheduling respawn...");
        IEnumerator respawnRoutine = WaitAndRespawn(enemyRespawnDelay, respawnShip);
        StartCoroutine(respawnRoutine);
    }
    
    private IEnumerator WaitAndRespawn(float delay, Ship respawnShip)
    {
        yield return new WaitForSeconds(delay);
        RespawnRandomEnemy(respawnShip);
    }
    
    private void RespawnRandomEnemy(Ship respawnShip)
    {
        respawnShip.ResetShip();
        respawnShip.ResetHealth();
        // Find a random offscreen position
        Vector3 respawnPosition = GetRandomOffscreenPosition();
        respawnShip.transform.position = respawnPosition;
        // Reactivate the ship
        respawnShip.gameObject.SetActive(true);
        
        Debug.Log($"Enemy ship respawned at position: {respawnPosition}");
    }
    
    private Vector3 GetRandomOffscreenPosition()
    {
        Vector3 pos = Random.insideUnitSphere.normalized * offscreenDistance + mainCamera.transform.position;
        pos.y = 0;
        return pos;
    }

    /// <summary>
    /// Reloads the current scene to restart the game.
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("Restarting game...");
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
        currentState = GameState.Playing;
    }
} 