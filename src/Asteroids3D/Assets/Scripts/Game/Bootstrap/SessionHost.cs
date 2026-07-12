using System;
using System.Collections;
using Game.Sectors;
using Game.Services;
using Movement.MPC.Field;
using Player;
using UnityEngine;
using Utils;

namespace Game.Bootstrap
{
    /// <summary>
    /// The below-seam half of the session tier: the driver-agnostic lifecycle primitives an
    /// interactive game or a headless/RL harness drives over an explicit per-session
    /// <see cref="GameSession"/>. The primitives compose the service container + optional player/camera/UI
    /// rig, cycle the sector, and tear the session down; they carry no clock and no reset policy — the
    /// driver above them supplies both. The dependency points UP only — a driver references the host; the
    /// host never references any driver.
    /// </summary>
    [RequireComponent(typeof(ObjectiveService))]
    [RequireComponent(typeof(UnitService))]
    [RequireComponent(typeof(NavFieldService))]
    public class SessionHost : MonoBehaviour, ISessionPrimitives
    {
        [Header("Session Rig")]
        [Tooltip("Session-tier player/camera/UI/world rig. Built once at Start; persists across sector restarts.")]
        [SerializeField] private SessionRig playerRig;

        [Header("Game Plane")]
        [SerializeField] private PlaneAxis planeAxis = PlaneAxis.Y;
        [SerializeField] private Vector3 planeOrigin;

        private UnitService unitService;
        private ObjectiveService objectiveService;
        private NavFieldService navFieldService;

        private void Awake()
        {
            unitService = GetComponent<UnitService>();
            objectiveService = GetComponent<ObjectiveService>();
            navFieldService = GetComponent<NavFieldService>();
        }

        /// <summary>
        /// Compose a session: service container, optional player/camera/UI rig, presentation
        /// overlay, and the universal world plane. Built once per session; the rig persists across
        /// sector loads and is removed only by <see cref="TeardownSession"/>.
        /// </summary>
        public IEnumerator ComposeSession(GameSession target)
        {
            // Game-tier VFX policy: gate the still-un-migrated weapon/projectile/asteroid effects.
            // Runtime-only override so a headless/RL session never leaks into the saved preference.
            GameSettings.SetVfxEnabled(target.Profile.vfx);

            // Presentation policy: when off (headless/RL), each ship's embedded visual rig
            // self-disables in its Awake. Runtime-only override, same lifetime as the VFX toggle.
            GameSettings.SetPresentationEnabled(target.Profile.presentation);

            // GamePlane is a process-global; guard so composing a second session (multi-arena)
            // shares the one plane instead of throwing on an already-configured Configure.
            if (!GamePlane.IsConfigured)
                GamePlane.Configure(planeAxis, planeOrigin);

            var arena = new ArenaContext(target.Profile.offset, unitService.Registry, navFieldService);
            unitService.SetArena(arena);

            target.Services = new GameServices(
                unitService: unitService,
                environmentService: new EnvironmentService(),
                objectiveService: objectiveService,
                cameraService: new CameraService(),
                uiService: new UIService(),
                arena: arena
            );

            if (playerRig)
            {
                target.Rig = playerRig;
                // Consume the driver-set hook; never overwrite it — a
                // headless/RL driver supplies its own OnPlayerDeath before composing.
                yield return playerRig.Build(target.Services, target.Profile.buildPlayer, target.OnPlayerDeath);
            }
        }

        /// <summary>
        /// Load the profile's sector into the session and subscribe the session's
        /// <see cref="GameSession.OnSectorComplete"/> policy hook to it.
        /// </summary>
        public IEnumerator LoadSector(GameSession target)
        {
            var entry = target.Profile.sectorEntry;
            if (!entry?.prefab)
                throw new InvalidOperationException("No sector entry configured on the session profile.");

            // Environment: make the sector's locale the active (lighting) scene before content builds.
            // Skipped headless (no presentation) and no-op when the locale is unassigned or unchanged.
            if (target.Profile.presentation)
                yield return target.Services.EnvironmentService.ApplyLocaleAsync(
                    entry.config ? entry.config.Locale?.SceneName : null);

            // Instantiate the sector subtree under an inactive holder so authored content children
            // do not Awake until services exist and adoption has wired each object. Setup runs
            // while inert; then reparent out (world pose preserved) and drop the holder so children
            // Awake post-wiring. Authoring stays WYSIWYG — only runtime instantiation is gated.
            var holder = new GameObject("SectorLoad") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);

            var sector = Instantiate(entry.prefab, holder.transform);
            target.ActiveSector = sector;
            // Inject the persistent rig's player — the sector references it, never builds/owns it.
            sector.Initialize(target.Services, entry.config, target.Rig ? target.Rig.Player : null);

            if (target.OnSectorComplete != null)
                sector.OnSectorComplete += target.OnSectorComplete;

            // Entry reset: place the persistent player at the sector's declared start (plane-space,
            // producer-relative to the sector so it's deterministic every load). The sector only
            // DECLARES the start via PlayerStart; the session tier does the reset.
            if (target.Rig && target.Rig.Player)
                target.Services.UnitService.RespawnShip(
                    target.Rig.Player.Id, sector.PlayerStart, 0f);

            yield return sector.Setup();

            sector.transform.SetParent(null, true);
            // Keep the sector — which runs the teardown coroutine — out of the swappable locale scene.
            if (target.Profile.presentation)
                target.Services.EnvironmentService.HomeToStableScene(sector.gameObject);
            Destroy(holder);
        }

        /// <summary>
        /// Unload the session's sector: cancel pending revives, run the sector's teardown phase,
        /// destroy its content. The session rig and service registries persist — only sector
        /// content cycles. Pair with <see cref="LoadSector"/> for a full episode reset.
        /// </summary>
        public IEnumerator UnloadSector(GameSession target)
        {
            // Drop any queued player/NPC revives so a pending respawn can't fire into the torn-down sector.
            target.Services.UnitService.CancelPendingRespawns();

            yield return DestroyActiveSector(target, runTeardown: true);
        }

        /// <summary>
        /// Session exit: drop the sector (without running its teardown phase), tear down the
        /// persistent rig, wipe every registry, and reset the world plane.
        /// </summary>
        public IEnumerator TeardownSession(GameSession target)
        {
            yield return DestroyActiveSector(target, runTeardown: false);

            if (target.Rig)
                target.Rig.Teardown();
            target.Rig = null;

            // Restore boot lighting + unload the locale after the rig is gone (the rig lives in the
            // boot scene, so the unload never touches it).
            if (target.Profile != null && target.Profile.presentation && target.Services != null)
                yield return target.Services.EnvironmentService.RestoreBootEnvironmentAsync();

            target.Services?.ClearAll();
            target.Services = null;

            GamePlane.Reset();
        }

        /// <summary>Install the session rig's standing loadout onto the persistent player.</summary>
        public void ApplyLoadout(GameSession session) => session.Rig?.ApplyLoadout();

        private IEnumerator DestroyActiveSector(GameSession target, bool runTeardown)
        {
            var sector = target.ActiveSector;
            if (sector)
            {
                if (target.OnSectorComplete != null)
                    sector.OnSectorComplete -= target.OnSectorComplete;

                if (runTeardown)
                    yield return sector.Teardown();

                Destroy(sector.gameObject);
                target.ActiveSector = null;
            }
        }

        private void OnDestroy()
        {
            GamePlane.Reset();
        }
    }
}
