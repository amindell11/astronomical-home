using Combat.Conditions;
using Ships;
using UnityEngine;
using UI.Audio;

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class Overlay : MonoBehaviour
    {
        [Header("Minimap")]
        [SerializeField] private RectTransform minimapRect;

        private Canvas canvas;
        private UILockOnAudio lockOnAudio;
        private UIHealthAudio healthAudio;
        private UILaserAudio laserAudio;
        private WeaponReadoutBuilder readoutBuilder;

        public MinimapObjectiveMarker ObjectiveMarker { get; private set; }
        public RectTransform MinimapRect => minimapRect;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            lockOnAudio = GetComponentInChildren<UILockOnAudio>();
            healthAudio = GetComponentInChildren<UIHealthAudio>();
            laserAudio = GetComponentInChildren<UILaserAudio>();
            readoutBuilder = GetComponentInChildren<WeaponReadoutBuilder>(true);
            ObjectiveMarker = GetComponentInChildren<MinimapObjectiveMarker>(true);
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

            if (readoutBuilder)
                readoutBuilder.Build(player.Weapons);

            // Overheat audio is a single overlay-level channel; it follows the first
            // heat-carrying weapon in slot order.
            if (laserAudio)
                laserAudio.Initialize(readoutBuilder ? readoutBuilder.FirstCondition<Heat>() : null);
        }
    }
}
