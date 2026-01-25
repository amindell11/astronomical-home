using Audio;
using Ships;
using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class Overlay : MonoBehaviour
    {
        private Canvas canvas;
        private UILockOnAudio lockOnAudio;
        private UIHealthAudio healthAudio;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            lockOnAudio = GetComponentInChildren<UILockOnAudio>();
            healthAudio = GetComponentInChildren<UIHealthAudio>();
        }

        public void SetCanvasWorldCamera(Camera uicam)
        {
            canvas.worldCamera = uicam;
        }

        public void Initialize(Ship player)
        {
            if (lockOnAudio && player.Targeting)
                lockOnAudio.Initialize(player.Targeting);

            if (healthAudio && player.Damage)
                healthAudio.Initialize(player.Damage);
        }
    }
}
