using System.Collections;
using Asteroids.Fields;
using Cameras;
using Game.Sectors.Utils;
using Ships;
using Ships.Command;
using UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using World;

namespace Game.Sectors
{
    /// <summary>
    /// The single concrete play-sector. Builds the world (imperative singleton infrastructure),
    /// player ship, observer camera, and UI overlay, then the base <see cref="Sector"/> template runs
    /// the manifest content (adopt + spawners) and behavior modules. Combat / Arena / Testbench are
    /// prefabs of this class — they differ only in their manifest (adopted ships, RingSpawners),
    /// their modules (EncounterSequence, Spectator), and producer-owned RespawnPolicies. The debug
    /// overlay installer is an editor-only partial (<c>PlaySector.Editor.cs</c>).
    /// </summary>
    public partial class PlaySector : Sector
    {
        [Header("Environment")]
        [SerializeField] private WorldRoot worldPrefab;
        [SerializeField] private UpdatingAsteroidField updatingAsteroidFieldPrefab;

        [Header("Player")]
        [SerializeField] private Ship playerTemplate;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private ShipSettings shipSettings;
        [SerializeField] private Vector2 playerSpawnPosition = Vector2.zero;

        [Tooltip("Optional player respawn rule (origin None = no respawn).")]
        [SerializeField] private RespawnPolicy playerRespawn;

        [Header("Camera")]
        [SerializeField] private ObserverCam observerCamPrefab;
        [SerializeField] private Camera uiCamPrefab;
        [SerializeField] private Camera minimapCamPrefab;

        [Header("UI")]
        [SerializeField] private Overlay overlayPrefab;

        private Ship player;
        private UpdatingAsteroidField asteroidFieldInstance;

        protected override IEnumerator OnBeforeContent()
        {
            // World is singleton infrastructure built before the player/camera, which depend on it.
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);

            SectorUtils.BuildAndWireObserverCam(Services, observerCamPrefab);

            player = SectorUtils.BuildAndWirePlayer(
                playerTemplate, playerCommander, shipSettings,
                0, playerSpawnPosition, Services);

            Respawn.Wire(player, playerRespawn, Services);

            var observer = Services.CameraService.GetCamera<ObserverCam>(CameraTag.Observer);

            if (overlayPrefab && uiCamPrefab)
            {
                var uiCam = Instantiate(uiCamPrefab, observer.transform);
                uiCam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                observer.Cam.GetUniversalAdditionalCameraData().cameraStack.Add(uiCam);

                var overlay = Instantiate(overlayPrefab);
                Services.UIService.Show(overlay, uiCam);
                overlay.Initialize(player);

                // Hand the objective marker the objective-service channel; it subscribes and
                // self-decides visibility (encounters report their target via IObjectiveService).
                if (overlay.ObjectiveMarker)
                    overlay.ObjectiveMarker.BindObjectiveService(Services.ObjectiveService);
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

        protected override IEnumerator OnAfterContent()
        {
            InitializeAsteroidField();
            InitializeDebugOverlay();
            yield break;
        }

        private void InitializeAsteroidField()
        {
            if (!updatingAsteroidFieldPrefab) return;

            var world = Services.EnvironmentService.World;
            var cullingBoundary = world ? world.AsteroidCullingBoundary : null;
            if (!cullingBoundary)
            {
                Debug.LogWarning("[PlaySector] Asteroid field prefab assigned but no AsteroidCullingBoundary in the world.");
                return;
            }

            asteroidFieldInstance = Instantiate(updatingAsteroidFieldPrefab);
            asteroidFieldInstance.Initialize(cullingBoundary);
            asteroidFieldInstance.SetWorldAnchor(Services.EnvironmentService.WorldFollowerTransform);
            asteroidFieldInstance.CurrentAnchorPos = () =>
                GetContentAnchorWorldPos(asteroidFieldInstance.transform.position);
        }

        protected override Vector3 GetContentAnchorWorldPos(Vector3 fallback) =>
            player ? GamePlane.ProjectOntoPlane(player.transform.position) : fallback;

        protected override Ship GetSectorPlayer() => player;

        protected override IEnumerator OnBeforeTeardown()
        {
            TeardownDebugOverlay();
            if (asteroidFieldInstance) Destroy(asteroidFieldInstance.gameObject);
            yield break;
        }

        protected override IEnumerator OnAfterTeardown()
        {
            Services.UIService.Clear();
            Services.CameraService.Clear();
            Services.UnitService.Clear();
            Services.EnvironmentService.Clear();

            player = null;
            asteroidFieldInstance = null;

            yield return null;
        }

        partial void InitializeDebugOverlay();
        partial void TeardownDebugOverlay();
    }
}
