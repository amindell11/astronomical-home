using System.Collections;
using Cameras;
using Game.Sectors.Utils;
using Ships;
using UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Sectors
{
    /// <summary>
    /// Base class for sectors that feature a player ship, observer camera, and UI overlay.
    /// Sits between SectorManager and concrete sector types (e.g. CombatSectorManager).
    /// </summary>
    public abstract class PlaySector : SectorManager
    {
        [Header("Player")]
        [SerializeField] protected Ship playerTemplate;
        [SerializeField] protected Ships.Command.Commander playerCommander;
        [SerializeField] protected ShipSettings shipSettings;
        [SerializeField] protected Vector2 playerSpawnPosition = Vector2.zero;

        [Header("Camera")]
        [SerializeField] private ObserverCam observerCamPrefab;
        [SerializeField] private Camera uiCamPrefab;
        [SerializeField] private Camera minimapCamPrefab;

        [Header("UI")]
        [SerializeField] private Overlay overlayPrefab;

        protected Ship player;

        protected override IEnumerator OnSetup()
        {
            SectorUtils.BuildAndWireObserverCam(Services, observerCamPrefab);

            player = SectorUtils.BuildAndWirePlayer(
                playerTemplate, playerCommander, shipSettings,
                0, playerSpawnPosition, Services);
            var observer = Services.CameraService.GetCamera<ObserverCam>(CameraTag.Observer);

            if (overlayPrefab && uiCamPrefab)
            {
                var uiCam = Instantiate(uiCamPrefab, observer.transform);
                uiCam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                observer.Cam.GetUniversalAdditionalCameraData().cameraStack.Add(uiCam);

                var overlay = Instantiate(overlayPrefab);
                Services.UIService.Show(overlay, uiCam);
                overlay.Initialize(player);
            }

            if (minimapCamPrefab)
            {
                var minimapCam = Instantiate(minimapCamPrefab, observer.transform);
                var overlay = Services.UIService.ActiveOverlay;
                if (overlay && overlay.ObjectiveMarker && overlay.MinimapRect)
                    overlay.ObjectiveMarker.Initialize(minimapCam, overlay.MinimapRect);
            }

            yield return null;
        }

        protected override IEnumerator OnTeardown()
        {
            Services.UIService.Clear();
            Services.CameraService.Clear();
            Services.UnitService.Clear();
            Services.EnvironmentService.Clear();

            player = null;

            yield return null;
        }
    }
}
