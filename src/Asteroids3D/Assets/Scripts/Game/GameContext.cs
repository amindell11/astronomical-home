using System.Collections;
using Ships;
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
        private UI.Overlay overlay;

        public static GameContext Instance => Singleton;

        public GameState CurrentState { get; private set; } = GameState.Playing;
        public ShipRegistry ShipRegistry { get; private set; }
        public WorldFollow WorldFollow { get; private set; }
        public Camera MainCamera { get; private set; }
        public IGamePlane Plane { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            gameInitiator = gameObject.AddComponent<GameInitiator>();
            gameInitiator.PresentationReady += HandlePresentationReady;
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

        public void SetPlane(IGamePlane plane)
        {
            Plane = plane ?? throw new System.ArgumentNullException(nameof(plane));
        }

        public void RestartGame()
        {
            PlayGame();
        }

        private void PlayGame()
        {
            CurrentState = GameState.Playing;
        }

        protected override void OnDestroy()
        {
            if (gameInitiator)
                gameInitiator.PresentationReady -= HandlePresentationReady;

            if (overlay)
                Destroy(overlay.gameObject);

            base.OnDestroy();
        }

        private void HandlePresentationReady(Ship player, Camera uiCamera)
        {
            if (!gameConfig || !gameConfig.UI || !player)
                return;

            if (overlay)
                Destroy(overlay.gameObject);

            overlay = Instantiate(gameConfig.UI);
            overlay.SetCanvasWorldCamera(uiCamera);
            overlay.Initialize(player);
        }
    }
}
