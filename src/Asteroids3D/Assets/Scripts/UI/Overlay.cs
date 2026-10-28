using Combat.Targeting;
using UnityEngine;
using UI.Audio;
using UI.Markers;
using UI.PlayerState;
using UI.Weapons;
using Combat.Weapons.Conditions;
using Substrate.Services.Objectives;
using Ships;
using Utils;

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class Overlay : MonoBehaviour
    {
        [Header("Minimap")]
        [SerializeField] private RectTransform minimapRect;
        [SerializeField] private AimCrosshair crosshair;

        private Canvas[] canvases;
        private CanvasGroup visibility;
        private UILockOnAudio lockOnAudio;
        private UIHealthAudio healthAudio;
        private UILaserAudio laserAudio;
        private UIBoostAudio boostAudio;
        private UIMissileWarningAudio missileWarningAudio;
        private BoostGaugeUI boostGauge;
        private WeaponReadoutBuilder readoutBuilder;
        private RunTallyReadout tallyReadout;
        private ChargeTargetMarkers chargeMarkers;
        private Camera gameplayCamera;
        private bool visible = true;
        private ShipStatusOverlay shipStatus;

        public MinimapObjectiveMarker ObjectiveMarker { get; private set; }
        public RectTransform MinimapRect => minimapRect;
        public Camera WorldCamera => canvases[0].worldCamera;

        private void Awake()
        {
            canvases = GetComponentsInChildren<Canvas>(true);
            visibility = gameObject.AddComponent<CanvasGroup>();
            lockOnAudio = GetComponentInChildren<UILockOnAudio>();
            healthAudio = GetComponentInChildren<UIHealthAudio>();
            laserAudio = GetComponentInChildren<UILaserAudio>();
            boostAudio = GetComponentInChildren<UIBoostAudio>();
            missileWarningAudio = GetComponentInChildren<UIMissileWarningAudio>();
            boostGauge = GetComponentInChildren<BoostGaugeUI>(true);
            readoutBuilder = GetComponentInChildren<WeaponReadoutBuilder>(true);
            ObjectiveMarker = GetComponentInChildren<MinimapObjectiveMarker>(true);
            chargeMarkers = GetComponentInChildren<ChargeTargetMarkers>(true);
        }

        public void ConfigureCameras(Camera uiCamera, Camera gameplayCamera)
        {
            // GetComponentsInChildren returns self first; RequireComponent guarantees a root canvas.
            canvases[0].worldCamera = uiCamera;
            this.gameplayCamera = gameplayCamera;
        }

        /// <summary>
        /// Toggles every canvas under the overlay, and the world-space charge markers — nested
        /// canvases (minimap) keep rendering when only the root canvas is disabled, and disabling
        /// GameObjects would break the HUD audio binders, which unsubscribe in OnDisable and never
        /// resubscribe.
        /// </summary>
        public void SetVisible(bool visible)
        {
            this.visible = visible;
            if (visible) SetOpacity(1f);
            foreach (var c in canvases)
                c.enabled = visible;
            if (chargeMarkers)
                chargeMarkers.SetVisible(visible);
            crosshair.gameObject.SetActive(visible && visibility.alpha > 0f);
        }

        public void SetOpacity(float opacity, float? shipOpacity = null)
        {
            visibility.alpha = opacity;
            if (chargeMarkers) chargeMarkers.SetVisible(opacity > 0f);
            crosshair.gameObject.SetActive(visible && opacity > 0f);
            if (shipStatus) shipStatus.SetOpacity(shipOpacity ?? opacity);
        }

        public void BindShips(SubscribedSet<Ship> ships, Camera worldCamera) =>
            shipStatus = ShipStatusOverlay.Create(canvases[0], ships, worldCamera);

        private void OnDestroy()
        {
            if (shipStatus) Destroy(shipStatus.gameObject);
        }

        public void Initialize(in HudBinding binding)
        {
            if (healthAudio)
                healthAudio.Initialize(binding.Damage);

            if (boostGauge)
                boostGauge.Initialize(binding.Status);

            if (boostAudio)
                boostAudio.Initialize(binding.Status);

            if (missileWarningAudio)
                missileWarningAudio.Initialize(binding.Status);

            if (readoutBuilder)
                readoutBuilder.Build(binding.Weapons);
            crosshair.Initialize(binding.Status, binding.Weapons, gameplayCamera);

            if (chargeMarkers)
                chargeMarkers.Initialize(binding.Weapons);

            if (!tallyReadout)
                tallyReadout = RunTallyReadout.Create(transform);
            tallyReadout.Initialize(binding.Tally);

            // Overheat and lock audio are single overlay-level channels; each follows the
            // first weapon (in slot order) that exposes the matching readout.
            if (laserAudio)
                laserAudio.Initialize(readoutBuilder ? readoutBuilder.FirstReadout<IHeatReadout>() : null);

            if (lockOnAudio)
                lockOnAudio.Initialize(readoutBuilder ? readoutBuilder.FirstReadout<ILockStateSource>() : null);
        }

        public void BindObjectives(IObjectiveService objectives)
        {
            if (ObjectiveMarker)
                ObjectiveMarker.BindObjectiveService(objectives);

            ObjectiveStatusReadout.Create(transform).Initialize(objectives);
        }
    }
}
