using System.Collections;
using Cameras;
using Game.Sectors;
using Game.Sectors.Utils;
using Game.Services;
using Ships;
using Ships.Command;
using UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using World;

namespace Game.Bootstrap
{
    /// <summary>
    /// Session-tier rig: builds the world (singleton infrastructure), player ship, observer camera and
    /// UI overlay <b>once</b> at <see cref="GameState.Start"/> and holds them for the whole session.
    /// Sectors are swapped underneath it and reference the player by injection
    /// (<see cref="Sector.Initialize"/>) — they never build or clear the rig. Only session exit tears
    /// the rig down. This is the code that used to live in <c>PlaySector.OnBeforeContent</c>, lifted up
    /// a tier so the player persists across sector restarts instead of being rebuilt each load.
    /// </summary>
    public partial class PlayerRig : MonoBehaviour
    {
        [Header("Environment")]
        [SerializeField] private WorldRoot worldPrefab;

        [Header("Player")]
        [SerializeField] private Ship playerTemplate;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private ShipSettings shipSettings;
        [SerializeField] private Vector2 playerSpawnPosition = Vector2.zero;

        [Tooltip("What happens when the player ship dies. RestartSector reloads the active sector; " +
                 "RespawnInPlace revives via playerRespawn; None does nothing.")]
        [SerializeField] private PlayerDeathBehavior deathBehavior = PlayerDeathBehavior.RestartSector;

        [Tooltip("Used when deathBehavior = RespawnInPlace.")]
        [SerializeField] private RespawnPolicy playerRespawn;

        [Header("Camera")]
        [SerializeField] private ObserverCam observerCamPrefab;
        [SerializeField] private Camera uiCamPrefab;
        [SerializeField] private Camera minimapCamPrefab;

        [Header("UI")]
        [SerializeField] private Overlay overlayPrefab;

        /// <summary>The persistent player ship, injected into each sector. Null until <see cref="Build"/>.</summary>
        public Ship Player { get; private set; }

        /// <summary>
        /// Build the world/player/camera/UI rig into the session services. Called once, before the
        /// first sector loads. Instances are owned by the services (Unit/Camera/UI/Environment) and
        /// therefore cleared by <c>services.ClearAll()</c> on session exit.
        /// </summary>
        public IEnumerator Build(IGameServices services, bool buildPlayer, System.Action onPlayerDeathRestart)
        {
            // World is singleton infrastructure built before the player/camera, which depend on it.
            if (worldPrefab)
                services.EnvironmentService.SpawnWorld(worldPrefab);

            SectorUtils.BuildAndWireObserverCam(services, observerCamPrefab);

            // Spectator/headless session: no player ship, no player-driven UI. The observer camera
            // already frames the fleet via the registry OnAdd/OnRemove wiring, so spectate works.
            if (!buildPlayer)
            {
                InitializeDebugOverlay(services);
                yield return null;
                yield break;
            }

            Player = SectorUtils.BuildAndWirePlayer(
                playerTemplate, playerCommander, shipSettings,
                0, playerSpawnPosition, services);

            // Session death policy: revive in place, restart the sector, or do nothing.
            switch (deathBehavior)
            {
                case PlayerDeathBehavior.RespawnInPlace:
                    Respawn.Wire(Player, playerRespawn, services);
                    break;
                case PlayerDeathBehavior.RestartSector:
                    if (Player && Player.Damage)
                        Player.Damage.OnDeath += (_, _) => onPlayerDeathRestart?.Invoke();
                    break;
                case PlayerDeathBehavior.None:
                default:
                    break;
            }

            var observer = services.CameraService.GetCamera<ObserverCam>(CameraTag.Observer);

            if (Player && overlayPrefab && uiCamPrefab)
            {
                var uiCam = Instantiate(uiCamPrefab, observer.transform);
                uiCam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                observer.Cam.GetUniversalAdditionalCameraData().cameraStack.Add(uiCam);

                var overlay = Instantiate(overlayPrefab);
                services.UIService.Show(overlay, uiCam);
                overlay.Initialize(Player);

                // Hand the objective marker the objective-service channel; it subscribes and
                // self-decides visibility (encounters report their target via IObjectiveService).
                if (overlay.ObjectiveMarker)
                    overlay.ObjectiveMarker.BindObjectiveService(services.ObjectiveService);
            }

            if (minimapCamPrefab)
            {
                var minimapCam = Instantiate(minimapCamPrefab, observer.transform);
                var overlay = services.UIService.ActiveOverlay;
                if (overlay && overlay.ObjectiveMarker && overlay.MinimapRect)
                    overlay.ObjectiveMarker.Initialize(minimapCam, overlay.MinimapRect);
            }

            InitializeDebugOverlay(services);

            yield return null;
        }

        /// <summary>
        /// Tear down rig-owned editor viz and drop the player reference. The service-owned instances
        /// (player, cameras, overlay, world) are destroyed by <c>services.ClearAll()</c> on exit.
        /// </summary>
        public void Teardown()
        {
            TeardownDebugOverlay();
            Player = null;
        }

        // Editor-only debug-overlay installer (PlayerRig.Editor.cs). No-op outside the editor.
        partial void InitializeDebugOverlay(IGameServices services);
        partial void TeardownDebugOverlay();
    }
}
