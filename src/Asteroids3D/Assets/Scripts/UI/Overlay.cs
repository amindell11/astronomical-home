using Combat.Targeting;
using UnityEngine;
using UI.Audio;
using UI.PlayerState;
using Combat.Weapons.Conditions;

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class Overlay : MonoBehaviour
    {
        [Header("Minimap")]
        [SerializeField] private RectTransform minimapRect;

        private Canvas[] canvases;
        private UILockOnAudio lockOnAudio;
        private UIHealthAudio healthAudio;
        private UILaserAudio laserAudio;
        private UIBoostAudio boostAudio;
        private BoostGaugeUI boostGauge;
        private WeaponReadoutBuilder readoutBuilder;

        public MinimapObjectiveMarker ObjectiveMarker { get; private set; }
        public RectTransform MinimapRect => minimapRect;

        private void Awake()
        {
            canvases = GetComponentsInChildren<Canvas>(true);
            lockOnAudio = GetComponentInChildren<UILockOnAudio>();
            healthAudio = GetComponentInChildren<UIHealthAudio>();
            laserAudio = GetComponentInChildren<UILaserAudio>();
            boostAudio = GetComponentInChildren<UIBoostAudio>();
            boostGauge = GetComponentInChildren<BoostGaugeUI>(true);
            readoutBuilder = GetComponentInChildren<WeaponReadoutBuilder>(true);
            ObjectiveMarker = GetComponentInChildren<MinimapObjectiveMarker>(true);
        }

        public void SetCanvasWorldCamera(Camera uicam)
        {
            // GetComponentsInChildren returns self first; RequireComponent guarantees a root canvas.
            canvases[0].worldCamera = uicam;
        }

        /// <summary>
        /// Toggles every canvas under the overlay — nested canvases (minimap) keep rendering when
        /// only the root canvas is disabled, and disabling GameObjects would break the HUD audio
        /// binders, which unsubscribe in OnDisable and never resubscribe.
        /// </summary>
        public void SetVisible(bool visible)
        {
            foreach (var c in canvases)
                c.enabled = visible;
        }

        public void Initialize(in HudBinding binding)
        {
            if (healthAudio)
                healthAudio.Initialize(binding.Damage);

            if (boostGauge)
                boostGauge.Initialize(binding.Status);

            if (boostAudio)
                boostAudio.Initialize(binding.Status);

            if (readoutBuilder)
                readoutBuilder.Build(binding.Weapons);

            // Overheat and lock audio are single overlay-level channels; each follows the
            // first weapon (in slot order) that exposes the matching readout.
            if (laserAudio)
                laserAudio.Initialize(readoutBuilder ? readoutBuilder.FirstReadout<IHeatReadout>() : null);

            if (lockOnAudio)
                lockOnAudio.Initialize(readoutBuilder ? readoutBuilder.FirstReadout<ILockStateSource>() : null);
        }
    }
}
