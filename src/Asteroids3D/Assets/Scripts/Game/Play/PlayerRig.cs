using System;
using System.Collections;
using Cameras;
using Damage;
using Game.Sectors;
using Game.Sessions;
using Game.Services;
using Player;
using Ships;
using Ships.Command;
using UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Ships.Registry;

namespace Game.Play
{
    /// <summary>
    /// Everything the interactive game puts into a session for the human: the player ship and its
    /// commander, the HUD (overlay, UI camera, minimap camera), the pending loadout and the damage
    /// ledger. Built <b>once</b> at session start against a viewport the host owns, and held for the
    /// whole session — sectors are swapped underneath it and reference the player by injection
    /// (<see cref="Sector.Initialize"/>), never building or clearing it. Pure mechanism: the rig holds
    /// no session policy — the host injects the player-death behavior via <see cref="Build"/> and the
    /// rig only wires it onto each player it builds. A host with no rig assigned has no player.
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Ship playerTemplate;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private Vector2 playerSpawnPosition = Vector2.zero;

        [Header("Camera")]
        [SerializeField] private Camera uiCamPrefab;
        [SerializeField] private Camera minimapCamPrefab;

        [Header("UI")]
        [SerializeField] private Overlay overlayPrefab;

        /// <summary>The persistent player ship, injected into each sector. Null until <see cref="Build"/>.</summary>
        public Ship Player { get; private set; }

        /// <summary>
        /// The player's pending module selection — seeded from the ship's authored build in
        /// <see cref="Build"/>, edited by the hangar, and installed by <see cref="ApplyLoadout"/> at
        /// each run's hangar step. Session-scoped; how session-scoped state should be owned across
        /// arenas is still an open question.
        /// </summary>
        public ShipLoadout Loadout { get; private set; }

        /// <summary>Per-life damage rows for the death recap; re-bound to each player the rig builds.</summary>
        public DamageLedger Ledger { get; } = new();

        /// <summary>The live HUD overlay this rig owns; null headless or before <see cref="Build"/>.</summary>
        public Overlay Overlay { get; private set; }

        // Session services captured at Build so the hangar can rebuild the player between runs.
        private IGameServices services;

        // The host's viewport, captured at Build so a rebuilt player can re-take the camera subject.
        private ObserverCam observer;

        // Host-supplied player-death behavior, stored at Build and wired onto every player the rig
        // builds (re-wired across RebuildPlayer). The rig owns no death policy — only this callback.
        private Action<ShipId, DamageInfo> onPlayerDeath;

        private SessionFrame frame;

        // The prefab the current Player instance was built from — a hangar ship change is detected
        // against this (the prefab is the archetype; see ShipLoadout.Ship).
        private Ship currentTemplate;

        /// <summary>
        /// Build the player and its HUD into the session services, framed by the host's
        /// <paramref name="observer"/>. Called once, before the first sector loads. The ship is owned
        /// by the unit service and therefore cleared by <c>services.ClearAll()</c> on session exit;
        /// the overlay is the rig's own and goes in <see cref="Teardown"/>. The host-supplied
        /// <paramref name="onPlayerDeath"/> is stored and wired onto the player synchronously at spawn
        /// (before any yield), so a spawn-frame death already has a subscriber.
        /// </summary>
        public IEnumerator Build(IGameServices services, ObserverCam observer, SessionFrame frame,
            Action<ShipId, DamageInfo> onPlayerDeath)
        {
            this.services = services;
            this.observer = observer;
            this.frame = frame;
            this.onPlayerDeath = onPlayerDeath;

            BuildPlayer(playerTemplate);

            Ledger.Bind(Player.Damage, services.UnitService.Registry);

            // Seed the pending loadout from the ship's authored build so an unedited hangar is a no-op.
            Loadout = new ShipLoadout(playerTemplate, Player.Engine, Player.Shield,
                Player.Weapons ? Player.Weapons.PrimaryMountPrefab : null,
                Player.Weapons ? Player.Weapons.SecondaryMountPrefab : null);

            // HUD/UI-cam/minimap are presentation: a headless/RL run builds no Canvas.
            if (services.PresentationEnabled && observer && overlayPrefab && uiCamPrefab)
            {
                var uiCam = Instantiate(uiCamPrefab, observer.transform);
                uiCam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                observer.Cam.GetUniversalAdditionalCameraData().cameraStack.Add(uiCam);

                Overlay = Instantiate(overlayPrefab);
                Overlay.SetCanvasWorldCamera(uiCam);
                Overlay.Initialize(BuildHudBinding());

                // Hand the objective marker the objective-service channel; it subscribes and
                // self-decides visibility (encounters report their target via IObjectiveService).
                if (Overlay.ObjectiveMarker)
                    Overlay.ObjectiveMarker.BindObjectiveService(services.ObjectiveService);
            }

            if (services.PresentationEnabled && observer && minimapCamPrefab)
            {
                var minimapCam = Instantiate(minimapCamPrefab, observer.transform);
                if (Overlay && Overlay.ObjectiveMarker && Overlay.MinimapRect)
                    Overlay.ObjectiveMarker.Initialize(minimapCam, Overlay.MinimapRect);
            }

            yield return null;
        }

        /// <summary>
        /// Drop the player reference, unwire its death callback and destroy the overlay. The
        /// service-owned player instance is destroyed by <c>services.ClearAll()</c> on exit.
        /// </summary>
        public void Teardown()
        {
            UnwirePlayerDeath();
            Ledger.Bind(null, null);
            if (Overlay)
                Destroy(Overlay.gameObject);
            Overlay = null;
            Player = null;
            observer = null;
            services = null;
        }

        /// <summary>
        /// Install the pending <see cref="Loadout"/> onto the persistent player ship. A module change
        /// is a data re-resolve (<see cref="Ship.Reequip"/>); a ship change is a whole-player rebuild
        /// (<see cref="RebuildPlayer"/>) followed by the module equip. Called at each run's
        /// hangar step — never mid-sector.
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
            // wired at spawn; ask it to re-wire (the player is never AI, so no field), then re-bind the HUD.
            services.UnitService.WireShipDependencies(Player, field: null);
            RebindHud();
        }

        /// <summary>
        /// Replace the persistent player with a fresh build of <paramref name="newTemplate"/>: despawn
        /// the old ship, re-run the standard player build/wiring (registry, camera subject, commander,
        /// screen-to-plane), and re-wire the injected death callback. The caller
        /// (<see cref="ApplyLoadout"/>) re-binds the HUD after the module equip that follows. Runs
        /// only in the between-run hangar gap, where no sector is loaded — the subsequent
        /// <c>LoadSector</c> injects and positions the new player as usual.
        /// </summary>
        private void RebuildPlayer(Ship newTemplate)
        {
            UnwirePlayerDeath();
            services.UnitService.DespawnShip(Player);

            BuildPlayer(newTemplate);

            Ledger.Bind(Player.Damage, services.UnitService.Registry);
        }

        // Spawn the ship, take the viewport's subject, and give the commander its screen-to-plane
        // projection; the death hook is wired here so a spawn-frame death already has a subscriber.
        private void BuildPlayer(Ship template)
        {
            // Player is team 0 by construction; only adopted enemies need a non-zero team.
            Player = services.UnitService.SpawnShip(
                template,
                playerCommander,
                0,
                frame.Place(playerSpawnPosition),
                GamePlane.Rotation,
                field: null);
            Player.tag = "Player";
            currentTemplate = template;

            WirePlayerDeath();

            if (!observer) return;

            observer.SetSubject(Player.transform);
            (Player.Commander as PlayerCommander)?.SetScreenToGamePlane(pos =>
                GamePlane.ProjectOntoPlane(observer.Cam.ScreenToWorldPoint(pos))
                + GamePlane.Origin);
        }

        // The overlay instance persists across player rebuilds and loadout changes; re-Initialize
        // re-binds every widget (readout builder clears and regenerates; audio binders unsubscribe
        // their old source).
        private void RebindHud()
        {
            if (Overlay)
                Overlay.Initialize(BuildHudBinding());
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
