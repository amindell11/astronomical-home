using Game.Play;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Full-screen splash canvas covering the non-interactive game states — boot, session compose,
    /// and sector load/unload. Instantiated once by <see cref="GameSessionHost"/> at boot and driven
    /// by its state transitions; the hangar and live-sector states hide it.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class LoadingSplash : MonoBehaviour
    {
        private Canvas canvas;
        private GameSessionHost host;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
        }

        public void Initialize(GameSessionHost host)
        {
            this.host = host;
            host.OnGameStateChanged += HandleGameStateChanged;
            HandleGameStateChanged(host.CurrentState);
        }

        private void OnDestroy()
        {
            if (host)
                host.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void HandleGameStateChanged(GameState state) => canvas.enabled = Covers(state);

        internal static bool Covers(GameState state) =>
            state is GameState.Loading or GameState.Start or GameState.LoadSector or GameState.Restart;
    }
}
