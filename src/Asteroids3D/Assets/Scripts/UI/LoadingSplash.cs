using Game;
using UnityEngine;

namespace UI
{
    /// <summary>Full-screen canvas <see cref="GameHost"/> shows over its non-interactive steps.</summary>
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
