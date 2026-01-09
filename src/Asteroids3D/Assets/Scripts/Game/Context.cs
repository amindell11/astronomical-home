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

    public class Context : MonoSingleton<Context>
    {
        [SerializeField] private Config config;
        private Initiator initiator;
        private Coroutine restartRoutine;
        
        public GameState CurrentState { get; private set; } = GameState.Playing;
        public Services Services { get; private set; }
        
        protected override void Awake()
        {
            base.Awake();
            initiator = gameObject.AddComponent<Initiator>();
            StartCoroutine(InitializeRoutine());
        }

        private IEnumerator InitializeRoutine()
        {
            yield return initiator.Initialize(config);
            Services = initiator.Services;
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
