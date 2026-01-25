using System.Collections;
using UnityEngine;
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

        public static GameContext Instance => Singleton;

        public GameState CurrentState { get; private set; } = GameState.Playing;
        public ShipRegistry ShipRegistry { get; private set; }
        
        protected override void Awake()
        {
            base.Awake();
            gameInitiator = gameObject.AddComponent<GameInitiator>();
            StartCoroutine(InitializeRoutine());
        }

        private IEnumerator InitializeRoutine()
        {
            yield return gameInitiator.Initialize(gameConfig);
            PlayGame();
        }

        public void SetRegistry(ShipRegistry registry)
        {
            ShipRegistry = registry;
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
