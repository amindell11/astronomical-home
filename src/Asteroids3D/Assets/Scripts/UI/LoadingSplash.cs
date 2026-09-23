using Game;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Full-screen splash canvas covering the game's non-interactive steps — boot, session compose,
    /// and sector load/unload. Instantiated once by <see cref="GameHost"/>, which shows and hides it
    /// around those steps; the hangar and live sector run uncovered.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class LoadingSplash : MonoBehaviour
    {
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
        }

        public void SetVisible(bool visible) => canvas.enabled = visible;
    }
}
