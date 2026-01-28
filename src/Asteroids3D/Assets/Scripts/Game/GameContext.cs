using System.Collections;
using UnityEngine;
using Utils;
using World;

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
        public WorldFollow WorldFollow { get; private set; }
        public Camera MainCamera { get; private set; }
        
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

        public void SetWorldFollow(WorldFollow worldFollow)
        {
            WorldFollow = worldFollow;
        }

        public void SetMainCamera(Camera camera)
        {
            MainCamera = camera;
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
