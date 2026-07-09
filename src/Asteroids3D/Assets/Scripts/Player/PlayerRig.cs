using System.Collections;
using Cameras;
using Game.Bootstrap;
using Game.Sectors;
using Game.Sectors.Utils;
using Game.Services;
using Ships;
using Ships.Command;
using UI;
using UnityEngine;
using Utils;
using UnityEngine.Rendering.Universal;
using World;

namespace Player
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
        /// <summary>
        /// Session policy for what happens when the persistent player ship dies.
        /// <see cref="None"/> = nothing, <see cref="RespawnInPlace"/> = revive via the rig's
        /// <see cref="playerRespawn"/>, <see cref="RestartSector"/> = tear down and reload the active
        /// sector (the rig persists).
        /// </summary>
        public enum PlayerDeathBehavior { None, RespawnInPlace, RestartSector }

        [Header("Environment")]
        [SerializeField] private WorldRoot worldPrefab;

        [Header("Player")]
        [SerializeField] private Ship playerTemplate;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private Vector2 playerSpawnPosition = Vector2.zero;

        [Tooltip("What happens when the player ship dies. RestartSector reloads the active sector; " +
                 "RespawnInPlace revives via playerRespawn; None does nothing.")]
        [SerializeField] private PlayerDeathBehavior deathBehavior = PlayerDeathBehavior.RestartSector;

        [Tooltip("Used when deathBehavior = RespawnInPlace.")]
        [SerializeField] private RespawnPolicy playerRespawn;

        [Tooltip("Modules the hangar offers per slot for the player build. Null → no hangar choices " +
                 "(the player flies its prefab-authored modules).")]
        [SerializeField] private LoadoutConfig loadoutCatalog;

        [Header("Camera")]
        [SerializeField] private ObserverCam observerCamPrefab;
        [SerializeField] private Camera uiCamPrefab;
        [SerializeField] private Camera minimapCamPrefab;

        [Header("UI")]
        [SerializeField] private Overlay overlayPrefab;

        [Tooltip("Between-run hangar screen. Null → no interactive hangar (the standing loadout is " +
                 "applied silently).")]
        [SerializeField] private HangarScreen hangarScreenPrefab;

        /// <summary>The persistent player ship, injected into each sector. Null until <see cref="Build"/>.</summary>
        public Ship Player { get; private set; }

        /// <summary>The modules the hangar can offer per slot. Null when no catalog is configured.</summary>
        public LoadoutConfig Catalog => loadoutCatalog;

        /// <summary>
        /// The player's pending module selection — seeded from the ship's authored build in
        /// <see cref="Build"/>, edited by the hangar, and installed by <see cref="ApplyLoadout"/> at
        /// each run's <see cref="GameState.Hangar"/> step. Session-scoped (this is the session rig);
        /// how session-scoped state should be owned across arenas is still an open question.
        /// Null in a spectator/headless session (no player).
        /// </summary>
        public ShipLoadout Loadout { get; private set; }

        /// <summary>
        /// Raised when the rig's death policy is <see cref="PlayerDeathBehavior.RestartSector"/> and
        /// the player ship dies. The rig only declares the request; the session driver decides what
        /// a restart means.
        /// </summary>
        public event System.Action RestartRequested;

        // Session services captured at Build so the hangar can rebuild the player between runs.
        private IGameServices services;

        // The prefab the current Player instance was built from — a hangar ship change is detected
        // against this (the prefab is the archetype; see ShipLoadout.Ship).
        private Ship currentTemplate;

        /// <summary>
        /// Build the world/player/camera/UI rig into the session services. Called once, before the
        /// first sector loads. Instances are owned by the services (Unit/Camera/UI/Environment) and
        /// therefore cleared by <c>services.ClearAll()</c> on session exit.
        /// </summary>
        public IEnumerator Build(IGameServices services, bool buildPlayer)
        {
            this.services = services;

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
                playerTemplate, playerCommander,
                0, playerSpawnPosition, services);
            currentTemplate = playerTemplate;

            // Seed the pending loadout from the ship's authored build so an unedited hangar is a no-op.
            Loadout = new ShipLoadout(playerTemplate, Player.Engine, Player.Shield);

            WireDeathPolicy();

            var observer = services.CameraService.GetCamera<ObserverCam>(CameraTag.Observer);

            if (Player && overlayPrefab && uiCamPrefab)
            {
                var uiCam = Instantiate(uiCamPrefab, observer.transform);
                uiCam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                observer.Cam.GetUniversalAdditionalCameraData().cameraStack.Add(uiCam);

                var overlay = Instantiate(overlayPrefab);
                services.UIService.Show(overlay, uiCam);
                // The HUD binds narrow read surfaces, never the Ship itself (see HudBinding).
                overlay.Initialize(new HudBinding(
                    Player, Player.Damage, Player.Weapons ? Player.Weapons.ReadoutContext : null));

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
            UnwireDeathPolicy();
            Player = null;
            services = null;
        }

        /// <summary>
        /// Install the pending <see cref="Loadout"/> onto the persistent player ship. A module change
        /// is a data re-resolve (<see cref="Ship.Reequip"/>); a ship change is a whole-player rebuild
        /// (<see cref="RebuildPlayer"/>) followed by the module equip. Called at each run's
        /// <see cref="GameState.Hangar"/> step — never mid-sector. No-op in a spectator/headless
        /// session (no player).
        /// </summary>
        public void ApplyLoadout()
        {
            if (!Player || Loadout == null) return;

            if (Loadout.Ship && Loadout.Ship != currentTemplate)
                RebuildPlayer(Loadout.Ship);

            Player.Reequip(Loadout.Engine, Loadout.Shield);
        }

        /// <summary>
        /// Replace the persistent player with a fresh build of <paramref name="newTemplate"/>: despawn
        /// the old ship, re-run the standard player build/wiring (registry, camera subject, world
        /// follower, commander, screen-to-plane), re-arm the death policy, and re-bind the persistent
        /// HUD overlay to the new ship. Runs only in the between-run hangar gap, where no sector is
        /// loaded — the subsequent <c>LoadSector</c> injects and positions the new player as usual.
        /// </summary>
        private void RebuildPlayer(Ship newTemplate)
        {
            UnwireDeathPolicy();
            services.UnitService.DespawnShip(Player);

            Player = SectorUtils.BuildAndWirePlayer(
                newTemplate, playerCommander,
                0, playerSpawnPosition, services);
            currentTemplate = newTemplate;

            WireDeathPolicy();

            // The overlay instance persists across the swap; re-Initialize re-binds every widget
            // (readout builder clears and regenerates; audio binders unsubscribe their old source).
            var overlay = services.UIService.ActiveOverlay;
            if (overlay)
                overlay.Initialize(new HudBinding(
                    Player, Player.Damage, Player.Weapons ? Player.Weapons.ReadoutContext : null));
        }

        // Session death policy: revive in place, restart the sector, or do nothing.
        private void WireDeathPolicy()
        {
            switch (deathBehavior)
            {
                case PlayerDeathBehavior.RespawnInPlace:
                    Respawn.Wire(Player, playerRespawn, services);
                    break;
                case PlayerDeathBehavior.RestartSector:
                    if (Player && Player.Damage)
                        Player.Damage.OnDeath += OnPlayerDeath;
                    break;
                case PlayerDeathBehavior.None:
                default:
                    break;
            }
        }

        private void UnwireDeathPolicy()
        {
            if (Player && Player.Damage)
                Player.Damage.OnDeath -= OnPlayerDeath;
        }

        /// <summary>
        /// Run the between-run hangar: show the screen, wait for the player to Launch, then install the
        /// chosen loadout. Skipped (standing selection applied silently) when there is no player, no
        /// screen, or presentation is off (headless/RL) — so an automated run never blocks on a click.
        /// </summary>
        public IEnumerator RunHangar()
        {
            if (!Player || Loadout == null || !hangarScreenPrefab || !GameSettings.PresentationEnabled)
            {
                ApplyLoadout();
                yield break;
            }

            // Blank the combat HUD canvas while the hangar is up (component lifecycles stay live —
            // some HUD audio binders unsubscribe on disable and never resubscribe).
            var overlay = services?.UIService.ActiveOverlay;
            var overlayCanvas = overlay ? overlay.GetComponent<Canvas>() : null;
            if (overlayCanvas) overlayCanvas.enabled = false;

            var screen = Instantiate(hangarScreenPrefab);
            var launched = false;
            screen.Show(loadoutCatalog, Loadout, () => launched = true);

            yield return new WaitUntil(() => launched);

            ApplyLoadout();
            Destroy(screen.gameObject);

            // ApplyLoadout may have rebuilt the player and re-bound the HUD; re-resolve the canvas.
            var activeOverlay = services?.UIService.ActiveOverlay;
            var activeCanvas = activeOverlay ? activeOverlay.GetComponent<Canvas>() : null;
            if (activeCanvas) activeCanvas.enabled = true;
        }

        private void OnPlayerDeath(ShipId victimId, ShipId killerId) => RestartRequested?.Invoke();

        // Editor-only debug-overlay installer (PlayerRig.Editor.cs). No-op outside the editor.
        partial void InitializeDebugOverlay(IGameServices services);
        partial void TeardownDebugOverlay();
    }
}
