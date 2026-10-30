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
using System;
using UI.Objectives;
using UI.Projection;

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class Overlay : MonoBehaviour
    {
        [Header("Minimap")]
        [SerializeField] private RectTransform minimapRect;
        [SerializeField] private Material textMaterial;
        [SerializeField] private AimCrosshair crosshair;
        [SerializeField] private WeaponResourceHud weaponResources;
        [SerializeField] private LockOnIndicator lockReticle;

        [Header("Typography")]
        [SerializeField] private Font labelFont;
        [SerializeField] private Font readoutFont;
        [SerializeField] private Font headingFont;
        [SerializeField] private Font timerFont;

        private Canvas[] canvases;
        private CanvasGroup visibility;
        private UILockOnAudio lockOnAudio;
        private UIHealthAudio healthAudio;
        private UILaserAudio laserAudio;
        private UIBoostAudio boostAudio;
        private UIMissileWarningAudio missileWarningAudio;
        private BoostGaugeUI boostGauge;
        private WeaponReadoutBuilder readoutBuilder;
        private ChargeTargetMarkers chargeMarkers;
        private Camera gameplayCamera;
        private bool visible = true;
        private ShipStatusOverlay shipStatus;
        private ObjectiveMarkers objectiveMarkers;
        private CanopyProjection projection;

        public Camera WorldCamera => canvases[0].worldCamera;
        public bool IsVisible => visible && visibility.alpha > 0f;

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
            objectiveMarkers = GetComponentInChildren<ObjectiveMarkers>(true);
            chargeMarkers = GetComponentInChildren<ChargeTargetMarkers>(true);
        }

        public void ConfigureCameras(Camera uiCamera, Camera gameplayCamera, CanopyProjection projection = null)
        {
            // GetComponentsInChildren returns self first; RequireComponent guarantees a root canvas.
            canvases[0].worldCamera = uiCamera;
            this.gameplayCamera = gameplayCamera;
            this.projection = projection;
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
            else if (shipStatus) shipStatus.SetOpacity(0f);
            foreach (var c in canvases)
                c.enabled = visible;
            if (chargeMarkers)
                chargeMarkers.SetVisible(visible);
            crosshair.gameObject.SetActive(visible && visibility.alpha > 0f);
            weaponResources.gameObject.SetActive(visible && visibility.alpha > 0f);
        }

        public void SetOpacity(float opacity, float? shipOpacity = null)
        {
            visibility.alpha = opacity;
            if (chargeMarkers) chargeMarkers.SetVisible(opacity > 0f);
            crosshair.gameObject.SetActive(visible && opacity > 0f);
            weaponResources.gameObject.SetActive(visible && opacity > 0f);
            if (shipStatus) shipStatus.SetOpacity(shipOpacity ?? opacity);
        }

        public void BindShips(SubscribedSet<Ship> ships, Camera worldCamera) =>
            shipStatus = ShipStatusOverlay.Create(canvases[0], ships, worldCamera, readoutFont, textMaterial,
                lockReticle, projection);

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
            crosshair.Initialize(binding.Status, gameplayCamera);
            weaponResources.Initialize(binding.Weapons);

            if (chargeMarkers)
                chargeMarkers.Initialize(binding.Weapons);

            // Overheat and lock audio are single overlay-level channels; each follows the
            // first weapon (in slot order) that exposes the matching readout.
            if (laserAudio)
                laserAudio.Initialize(readoutBuilder ? readoutBuilder.FirstReadout<IHeatReadout>() : null);

            if (lockOnAudio)
                lockOnAudio.Initialize(readoutBuilder ? readoutBuilder.FirstReadout<ILockStateSource>() : null);
        }

        public void BindObjectives(IObjectiveService objectives, Camera worldCamera, Camera minimapCamera)
        {
            if (!objectiveMarkers || !minimapRect)
                throw new InvalidOperationException($"Overlay on '{name}' needs its objective markers and minimap wired.");
            objectiveMarkers.Initialize(objectives, worldCamera, minimapCamera, minimapRect, canvases[0], projection, () => IsVisible);
            ObjectiveStatusReadout.Create(transform, labelFont, headingFont, timerFont, textMaterial,
                projection, () => IsVisible).Initialize(objectives);
        }
    }
}
