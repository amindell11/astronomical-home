using UnityEngine;
using UnityEngine.SceneManagement;

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

    private GameState currentState = GameState.Playing;
    public GameState CurrentState => currentState;

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
        Shader.WarmupAllShaders();
    }

    /// <summary>
    /// Call this when the player's ship has been destroyed.
    /// </summary>
    public void HandlePlayerDeath()
    {
        if (currentState == GameState.GameOver) return;

        currentState = GameState.GameOver;
        Debug.Log("Player ship destroyed. Game Over!");
        Invoke(nameof(RestartGame), restartDelay);
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