using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Utils;
using ShipMain;

public enum GameState
{
    Playing,
    GameOver
}

public class MainGameContext : MonoSingleton<MainGameContext>
{   
    public GameState CurrentState { get; private set; } = GameState.Playing;
    public void RestartGame()
    {
        var currentScene = SceneManager.GetActiveScene();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(currentScene.buildIndex);
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayGame();
    }
    private void PlayGame()
    {
        CurrentState = GameState.Playing;        
    }
}