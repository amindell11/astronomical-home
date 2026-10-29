using Combat.Targeting;
using UnityEngine;
using UI.Audio;
using UI.Markers;
using UI.PlayerState;
using Combat.Weapons.Conditions;
using Substrate.Services.Objectives;
using Ships;
using Utils;
using System;
using UI.Objectives;

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class Overlay : MonoBehaviour
    {
        [Header("Minimap")]
        [SerializeField] private RectTransform minimapRect;

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
        private ShipStatusOverlay shipStatus;
        private ObjectiveMarkers objectiveMarkers;

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
            objectiveMarkers = GetComponentInChildren<ObjectiveMarkers>(true);
            chargeMarkers = GetComponentInChildren<ChargeTargetMarkers>(true);
        }

        public void SetCanvasWorldCamera(Camera uicam)
        {
            // GetComponentsInChildren returns self first; RequireComponent guarantees a root canvas.
            canvases[0].worldCamera = uicam;
        }

        /// <summary>
        /// Toggles every canvas under the overlay, and the world-space charge markers — nested
        /// canvases (minimap) keep rendering when only the root canvas is disabled, and disabling
        /// GameObjects would break the HUD audio binders, which unsubscribe in OnDisable and never
        /// resubscribe.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (visible) SetOpacity(1f);
            foreach (var c in canvases)
                c.enabled = visible;
            if (chargeMarkers)
                chargeMarkers.SetVisible(visible);
        }

        public void SetOpacity(float opacity, float? shipOpacity = null)
        {
            visibility.alpha = opacity;
            if (chargeMarkers) chargeMarkers.SetVisible(opacity > 0f);
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

        public void BindObjectives(IObjectiveService objectives, Camera worldCamera, Camera minimapCamera)
        {
            if (!objectiveMarkers || !minimapRect)
                throw new InvalidOperationException($"Overlay on '{name}' needs its objective markers and minimap wired.");
            objectiveMarkers.Initialize(objectives, worldCamera, minimapCamera, minimapRect, canvases[0]);
            ObjectiveStatusReadout.Create(transform).Initialize(objectives);
        }
    }
}
