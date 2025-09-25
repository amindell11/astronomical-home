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
        [SerializeField] private GameInitiatorConfig config;
        private GameInitiator initiator;
        
        public GameState CurrentState { get; private set; } = GameState.Playing;

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(config);
            
            initiator = gameObject.AddComponent<GameInitiator>();
            StartCoroutine(initiator.Initialize(config));
        }
        
        public void RestartGame()
        {
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PlayGame();
        }
        private void PlayGame()
        {
            CurrentState = GameState.Playing;        
        }
    }
}
