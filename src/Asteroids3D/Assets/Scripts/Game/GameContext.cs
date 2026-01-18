using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

namespace Game
{
    public enum GameState
    {
        Playing,
        GameOver
    }

    public class GameContext : MonoSingleton<GameContext>
    {
        [SerializeField] private GameConfig gameConfig;
        private GameInitiator gameInitiator;
        private Coroutine restartRoutine;
        
        public GameState CurrentState { get; private set; } = GameState.Playing;
        public Services Services { get; private set; }
        
        protected override void Awake()
        {
            base.Awake();
            gameInitiator = gameObject.AddComponent<GameInitiator>();
            StartCoroutine(InitializeRoutine());
        }

        private IEnumerator InitializeRoutine()
        {
            yield return gameInitiator.Initialize(gameConfig);
            Services = gameInitiator.Services;
            PlayGame();
        }
        
        public void RestartGame()
        {
            PlayGame();
        }

        
        private void PlayGame()
        {
            CurrentState = GameState.Playing;        
        }
    }
}
