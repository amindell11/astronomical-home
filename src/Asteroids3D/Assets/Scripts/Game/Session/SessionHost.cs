using System;
using System.Collections;
using Game.Sectors;
using Game.Services;
using Player;
using UnityEngine;
using Utils;
using Game.Services.Units;
using Game.Services.UI;
using Game.Services.Objectives;
using Game.Services.Environment;
using Game.Services.Camera;

namespace Game.Session
{
    /// <summary>
    /// The below-seam half of the session tier: the lifecycle primitives <see cref="GameDriver"/> drives
    /// over an explicit per-session <see cref="GameSession"/>. The primitives compose the service container
    /// + optional player/camera/UI rig, cycle the sector, and tear the session down; they carry no clock
    /// and no reset policy — the driver above them supplies both. The dependency points UP only — a driver
    /// references the host; the host never references any driver. The RL harness does not drive these: it
    /// composes the substrate under them directly through <see cref="Services.ShipServices"/>.
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
            GameSettings.SetPresentationEnabled(target.Profile.presentation);

            // The session root doubles as the arena root: placed at the profile offset before anything composes against it.
            transform.position = GamePlane.Origin + GamePlane.PlaneDirToWorld(target.Profile.offset);

            var projectiles = ShipServices.Compose(unitService, transform, target.Profile.presentation);
            // The rig's player spawns before any sector loads, so a world must already exist.
            target.World = NoRocksWorld(target);

            target.Services = new GameServices(
                unitService: unitService,
                projectiles: projectiles,
                environmentService: new EnvironmentService(transform, target.Profile.presentation),
                objectiveService: objectiveService,
                cameraService: new CameraService(),
                uiService: new UIService(),
                presentationEnabled: target.Profile.presentation
            );

            if (playerRig)
            {
                target.Rig = playerRig;
                // Consume the driver-set OnPlayerDeath hook; never overwrite it.
                yield return playerRig.Build(target.Services, target.Profile.buildPlayer, target.World, target.OnPlayerDeath);
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
            target.World = new WorldHandle(target.Profile.offset, unitService.Registry, sector.ObstacleField);
            // Inject the persistent rig's player — the sector references it, never builds/owns it.
            sector.Initialize(target.Services, entry.config, target.World, target.Rig ? target.Rig.Player : null);

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
            target.World = NoRocksWorld(target);
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
            target.World = null;
        }

        private WorldHandle NoRocksWorld(GameSession target) =>
            new(target.Profile.offset, unitService.Registry, obstacleField: null);

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
