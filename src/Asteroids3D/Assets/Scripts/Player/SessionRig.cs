using System.Collections;
using Cameras;
using Damage;
using Game.Bootstrap;
using Game.Sectors;
using Game.Sectors.Utils;
using Game.Services;
using Ships;
using Ships.Command;
using UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using World;

namespace Player
{
    /// <summary>
    /// Session-tier rig: builds the world (singleton infrastructure), player ship, observer camera and
    /// UI overlay <b>once</b> at <see cref="GameState.Start"/> and holds them for the whole session.
    /// Sectors are swapped underneath it and reference the player by injection
    /// (<see cref="Sector.Initialize"/>) — they never build or clear the rig. Only session exit tears
    /// the rig down. Pure mechanism: the rig holds no session policy — the driver injects the
    /// player-death behavior via <see cref="Build"/> and the rig only wires it onto each player it builds.
    /// </summary>
    public class SessionRig : MonoBehaviour
    {
        [Header("Environment")]
        [SerializeField] private WorldRoot worldPrefab;

        [Header("Player")]
        [SerializeField] private Ship playerTemplate;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private Vector2 playerSpawnPosition = Vector2.zero;

        [Header("Camera")]
        [SerializeField] private ObserverCam observerCamPrefab;
        [SerializeField] private Camera uiCamPrefab;
        [SerializeField] private Camera minimapCamPrefab;

        [Header("UI")]
        [SerializeField] private Overlay overlayPrefab;

        /// <summary>The persistent player ship, injected into each sector. Null until <see cref="Build"/>.</summary>
        public Ship Player { get; private set; }

        /// <summary>
        /// The player's pending module selection — seeded from the ship's authored build in
        /// <see cref="Build"/>, edited by the hangar, and installed by <see cref="ApplyLoadout"/> at
        /// each run's <see cref="GameState.Hangar"/> step. Session-scoped (this is the session rig);
        /// how session-scoped state should be owned across arenas is still an open question.
        /// Null in a spectator/headless session (no player).
        /// </summary>
        public ShipLoadout Loadout { get; private set; }

        /// <summary>Per-life damage rows for the death recap; re-bound to each player the rig builds.</summary>
        public DamageLedger Ledger { get; } = new();

        // Session services captured at Build so the hangar can rebuild the player between runs.
        private IGameServices services;

        // Driver-supplied player-death behavior, stored at Build and wired onto every player the rig
        // builds (re-wired across RebuildPlayer). The rig owns no death policy — only this callback.
        private System.Action<ShipId, DamageInfo> onPlayerDeath;

        // The prefab the current Player instance was built from — a hangar ship change is detected
        // against this (the prefab is the archetype; see ShipLoadout.Ship).
        private Ship currentTemplate;

        /// <summary>
        /// Build the world/player/camera/UI rig into the session services. Called once, before the
        /// first sector loads. Instances are owned by the services (Unit/Camera/UI/Environment) and
        /// therefore cleared by <c>services.ClearAll()</c> on session exit. The driver-supplied
        /// <paramref name="onPlayerDeath"/> is stored and wired onto the player synchronously at spawn
        /// (before any yield), so a spawn-frame death already has a subscriber.
        /// </summary>
        public IEnumerator Build(IGameServices services, bool buildPlayer,
            System.Action<ShipId, DamageInfo> onPlayerDeath)
        {
            this.services = services;
            this.onPlayerDeath = onPlayerDeath;

            // World is singleton infrastructure built before the player/camera, which depend on it.
            if (worldPrefab)
                services.EnvironmentService.SpawnWorld(worldPrefab);

            SectorUtils.BuildAndWireObserverCam(services, observerCamPrefab);

            // Spectator/headless session: no player ship, no player-driven UI. The observer camera
            // already frames the fleet via the registry OnAdd/OnRemove wiring, so spectate works.
            if (!buildPlayer)
            {
                yield return null;
                yield break;
            }

            Player = SectorUtils.BuildAndWirePlayer(
                playerTemplate, playerCommander,
                0, playerSpawnPosition, services);
            currentTemplate = playerTemplate;

            WirePlayerDeath();
            Ledger.Bind(Player.Damage, services.UnitService.Registry);

            // Seed the pending loadout from the ship's authored build so an unedited hangar is a no-op.
            Loadout = new ShipLoadout(playerTemplate, Player.Engine, Player.Shield,
                Player.Weapons ? Player.Weapons.PrimaryMountPrefab : null,
                Player.Weapons ? Player.Weapons.SecondaryMountPrefab : null);

            var observer = services.CameraService.GetCamera<ObserverCam>(CameraTag.Observer);

            // HUD/UI-cam/minimap are presentation: a headless/RL run builds no Canvas.
            if (services.PresentationEnabled && Player && overlayPrefab && uiCamPrefab)
            {
                var uiCam = Instantiate(uiCamPrefab, observer.transform);
                uiCam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                observer.Cam.GetUniversalAdditionalCameraData().cameraStack.Add(uiCam);

                var overlay = Instantiate(overlayPrefab);
                services.UIService.Show(overlay, uiCam);
                overlay.Initialize(BuildHudBinding());

                // Hand the objective marker the objective-service channel; it subscribes and
                // self-decides visibility (encounters report their target via IObjectiveService).
                if (overlay.ObjectiveMarker)
                    overlay.ObjectiveMarker.BindObjectiveService(services.ObjectiveService);
            }

            if (services.PresentationEnabled && minimapCamPrefab)
            {
                var minimapCam = Instantiate(minimapCamPrefab, observer.transform);
                var overlay = services.UIService.ActiveOverlay;
                if (overlay && overlay.ObjectiveMarker && overlay.MinimapRect)
                    overlay.ObjectiveMarker.Initialize(minimapCam, overlay.MinimapRect);
            }

            yield return null;
        }

        /// <summary>
        /// Drop the player reference and unwire its death callback. The service-owned instances
        /// (player, cameras, overlay, world) are destroyed by <c>services.ClearAll()</c> on exit.
        /// </summary>
        public void Teardown()
        {
            UnwirePlayerDeath();
            Ledger.Bind(null, null);
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

            // A new run starts here; the previous life's recap has already consumed the rows.
            Ledger.Clear();

            // A dead player reaches the hangar deactivated (death disables the ship GameObject).
            // Revive it before applying so swapped-in weapon mounts instantiate active and Awake-wire
            // like on the alive path; the subsequent LoadSector repositions and resets it anyway.
            if (!Player.gameObject.activeSelf)
                Player.ResetShip();

            if (Loadout.Ship && Loadout.Ship != currentTemplate)
                RebuildPlayer(Loadout.Ship);

            Player.Reequip(Loadout.Engine, Loadout.Shield,
                Loadout.PrimaryWeapon, Loadout.SecondaryWeapon);

            // Swapped-in weapon mounts carry world-facing parts (lock sensor) that the service
            // wired at spawn; ask it to re-wire, then re-bind the HUD to the new readouts.
            services.UnitService.WireShipDependencies(Player);
            RebindHud();
        }

        /// <summary>
        /// Replace the persistent player with a fresh build of <paramref name="newTemplate"/>: despawn
        /// the old ship, re-run the standard player build/wiring (registry, camera subject, world
        /// follower, commander, screen-to-plane), and re-wire the injected death callback. The caller
        /// (<see cref="ApplyLoadout"/>) re-binds the HUD after the module equip that follows. Runs
        /// only in the between-run hangar gap, where no sector is loaded — the subsequent
        /// <c>LoadSector</c> injects and positions the new player as usual.
        /// </summary>
        private void RebuildPlayer(Ship newTemplate)
        {
            UnwirePlayerDeath();
            services.UnitService.DespawnShip(Player);

            Player = SectorUtils.BuildAndWirePlayer(
                newTemplate, playerCommander,
                0, playerSpawnPosition, services);
            currentTemplate = newTemplate;

            WirePlayerDeath();
            Ledger.Bind(Player.Damage, services.UnitService.Registry);
        }

        // The overlay instance persists across player rebuilds and loadout changes; re-Initialize
        // re-binds every widget (readout builder clears and regenerates; audio binders unsubscribe
        // their old source).
        private void RebindHud()
        {
            var overlay = services.UIService.ActiveOverlay;
            if (overlay)
                overlay.Initialize(BuildHudBinding());
        }

        // The HUD binds narrow read surfaces, never the Ship itself (see HudBinding).
        private HudBinding BuildHudBinding() => new HudBinding(
            Player, Player.Damage, Player.Weapons ? Player.Weapons.ReadoutContext : null);

        private void WirePlayerDeath()
        {
            if (onPlayerDeath != null && Player && Player.Damage)
                Player.Damage.OnDeath += onPlayerDeath;
        }

        private void UnwirePlayerDeath()
        {
            if (onPlayerDeath != null && Player && Player.Damage)
                Player.Damage.OnDeath -= onPlayerDeath;
        }
    }
}
