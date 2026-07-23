using System;
using System.Collections;
using Game.Sectors;
using Game.Services;
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
    public class SessionHost : MonoBehaviour, ISessionPrimitives
    {
        [Header("Session Rig")]
        [Tooltip("Session-tier player/camera/UI/world rig. Built once at Start; persists across sector restarts.")]
        [SerializeField] private SessionRig playerRig;

        private UnitService unitService;
        private ObjectiveService objectiveService;

        private void Awake()
        {
            unitService = GetComponent<UnitService>();
            objectiveService = GetComponent<ObjectiveService>();
        }

        /// <summary>Compose the session's service container and optional rig — once per session; the rig persists across sector loads until <see cref="TeardownSession"/>.</summary>
        public IEnumerator ComposeSession(GameSession target)
        {
            // Runtime-only overrides so a headless/RL session never leaks into the saved preferences.
            // VFX is a sub-capability of presentation: no presentation ⇒ no VFX, whatever the profile says.
            GameSettings.SetVfxEnabled(target.Profile.vfx && target.Profile.presentation);
            GameSettings.SetPresentationEnabled(target.Profile.presentation);

            // The session root doubles as the arena root: placed at the profile offset before anything composes against it.
            transform.position = GamePlane.Origin + GamePlane.PlaneDirToWorld(target.Profile.offset);

            var arena = new ArenaContext(target.Profile.offset, unitService.Registry);
            unitService.SetArena(arena);

            var projectiles = new ProjectileService(transform, target.Profile.presentation);
            unitService.SetProjectiles(projectiles);

            target.Services = new GameServices(
                unitService: unitService,
                projectiles: projectiles,
                environmentService: new EnvironmentService(transform),
                objectiveService: objectiveService,
                cameraService: new CameraService(),
                uiService: new UIService(),
                arena: arena,
                presentationEnabled: target.Profile.presentation
            );

            if (playerRig)
            {
                target.Rig = playerRig;
                // Consume the driver-set OnPlayerDeath hook; never overwrite it.
                yield return playerRig.Build(target.Services, target.Profile.buildPlayer, target.OnPlayerDeath);
            }
        }

        /// <summary>Load the profile's sector and subscribe the session's <see cref="GameSession.OnSectorComplete"/> policy hook to it.</summary>
        public IEnumerator LoadSector(GameSession target)
        {
            var entry = target.Profile.sectorEntry;
            if (!entry?.prefab)
                throw new InvalidOperationException("No sector entry configured on the session profile.");

            // Make the sector's locale the active (lighting) scene before content builds; skipped headless.
            if (target.Profile.presentation)
                yield return target.Services.EnvironmentService.ApplyLocaleAsync(
                    entry.config ? entry.config.Locale?.SceneName : null);

            // Compose under an inactive holder at the arena root so authored children Awake only after adoption has wired them.
            var holder = new GameObject("SectorLoad") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);

            var sector = Instantiate(entry.prefab, holder.transform);
            target.ActiveSector = sector;
            // Inject the persistent rig's player — the sector references it, never builds/owns it.
            sector.Initialize(target.Services, entry.config, target.Rig ? target.Rig.Player : null);

            if (target.OnSectorComplete != null)
                sector.OnSectorComplete += target.OnSectorComplete;

            // The sector only DECLARES its start via PlayerStart; the session tier does the entry reset.
            if (target.Rig && target.Rig.Player)
                target.Services.UnitService.RespawnShip(
                    target.Rig.Player.Id, sector.PlayerStart, 0f);

            yield return sector.Setup();

            // Adopting into the arena root also moves the sector to the root's stable scene, keeping it out of the swappable locale scene.
            sector.transform.SetParent(transform, true);
            Destroy(holder);
        }

        /// <summary>Unload the sector (run its teardown phase, destroy its content); the rig and registries persist — pair with <see cref="LoadSector"/> for an episode reset.</summary>
        public IEnumerator UnloadSector(GameSession target)
        {
            // Drop any queued player/NPC revives so a pending respawn can't fire into the torn-down sector.
            target.Services.UnitService.CancelPendingRespawns();
            // Old-sector transients must not survive into the next sector (they live under the session root, not the sector).
            target.Services.Projectiles.ReturnAllToPool();

            yield return DestroyActiveSector(target, runTeardown: true);
        }

        /// <summary>Session exit: drop the sector (without running its teardown phase), tear down the persistent rig, and wipe every registry.</summary>
        public IEnumerator TeardownSession(GameSession target)
        {
            yield return DestroyActiveSector(target, runTeardown: false);

            if (target.Rig)
                target.Rig.Teardown();
            target.Rig = null;

            // Restore boot lighting + unload the locale after the rig (a boot-scene object) is gone.
            if (target.Profile != null && target.Profile.presentation && target.Services != null)
                yield return target.Services.EnvironmentService.RestoreBootEnvironmentAsync();

            target.Services?.ClearAll();
            target.Services = null;
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
    }
}
