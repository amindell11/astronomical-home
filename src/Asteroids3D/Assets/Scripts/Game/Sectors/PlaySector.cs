using System.Collections;
using Cameras;
using Game.Sectors.Utils;
using Ships;
using UI;
using UnityEngine;

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

        [Header("UI")]
        [SerializeField] private Overlay overlayPrefab;

        protected Ship player;

        protected override IEnumerator OnSetup()
        {
            SectorUtils.BuildAndWireObserverCam(Services, observerCamPrefab);

            player = SectorUtils.BuildAndWirePlayer(
                playerTemplate, playerCommander, shipSettings,
                0, playerSpawnPosition, Services);

            if (overlayPrefab)
            {
                var overlay = Instantiate(overlayPrefab);
                var observer = Services.CameraService.GetCamera<ObserverCam>(CameraTag.Observer);
                Services.UIService.Show(overlay, observer.Cam);
                overlay.Initialize(player);
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
