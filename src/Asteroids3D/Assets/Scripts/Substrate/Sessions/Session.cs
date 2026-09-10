using System;
using System.Collections;
using Substrate.Sectors;
using Substrate.Services;
using Ships;
using UnityEngine;
using Utils;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using Substrate.Services.Objectives;
using Substrate.Services.Environment;

namespace Substrate.Sessions
{
    /// <summary>
    /// One game session, orchestrating its own lifecycle over the substrate it is handed: it composes
    /// its services once, loads and unloads the profile's sector any number of times, and
    /// tears everything down. It owns no player and no policy — a host (<c>GameSessionHost</c> for the
    /// interactive game) paces these steps, owns the clock, hangar, death and restart, and hands the
    /// player it built to each load. The RL harness composes the same per-ship services
    /// through <see cref="ShipServices"/> and never drives a session. No process-wide state is written
    /// except the presentation flag set on compose, so one process can hold several sessions.
    /// </summary>
    public sealed class Session
    {
        private enum Phase { Created, Composed, Loaded, TornDown }

        private readonly Transform root;
        private readonly UnitService units;
        private readonly ObjectiveService objectives;
        private readonly LocaleService locale = new();
        private Action<SectorResult> onSectorComplete;
        private Phase phase = Phase.Created;

        public SessionProfile Profile { get; }

        /// <summary>The in-plane frame this session's authored content is placed in.</summary>
        public SessionFrame Frame { get; }

        /// <summary>Unit registry owned by this session; null once torn down.</summary>
        public IUnitService Units { get; private set; }

        /// <summary>Projectile pool owned by this session; null once torn down.</summary>
        public IProjectileService Projectiles { get; private set; }

        /// <summary>Objective registry owned by this session; null once torn down.</summary>
        public IObjectiveService Objectives { get; private set; }

        public Sector ActiveSector { get; private set; }

        public Session(SessionProfile profile, Transform root, UnitService units, ObjectiveService objectives)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.root = root ? root : throw new ArgumentNullException(nameof(root));
            this.units = units ? units : throw new ArgumentNullException(nameof(units));
            this.objectives = objectives ? objectives : throw new ArgumentNullException(nameof(objectives));
            Frame = new SessionFrame(profile.offset);
        }

        /// <summary>Compose the services — once; they persist across sector loads until <see cref="Teardown"/>.</summary>
        public IEnumerator Compose()
        {
            Require(Phase.Created, nameof(Compose));
            GameSettings.SetPresentationEnabled(Profile.presentation);

            // The session root doubles as the arena root: placed at the frame offset before anything composes against it.
            root.position = GamePlane.Origin + GamePlane.PlaneDirToWorld(Frame.Offset);

            Projectiles = ShipServices.Compose(units, root, Profile.presentation);
            Units = units;
            Objectives = objectives;

            phase = Phase.Composed;
            yield break;
        }

        /// <summary>
        /// Load the profile's sector, inject the host's hero into it, subscribe
        /// <paramref name="onSectorComplete"/> for the life of this load, and reset the hero to the
        /// sector's declared start. The hero is the main character the sector lays out around — the
        /// player today, possibly an AI; the sector side still names it the player. Every argument is
        /// optional: a headless session loads with none.
        /// </summary>
        public IEnumerator LoadSector(Ship hero = null, Action<SectorResult> onSectorComplete = null)
        {
            Require(Phase.Composed, nameof(LoadSector));
            var entry = Profile.sectorEntry;
            if (!entry?.prefab)
                throw new InvalidOperationException("No sector entry configured on the session profile.");

            // Make the sector's locale the active (lighting) scene before content builds; skipped headless.
            if (Profile.presentation)
                yield return locale.ApplyLocaleAsync(entry.config ? entry.config.Locale?.SceneName : null);

            // Compose under an inactive holder at the arena root so authored children Awake only after adoption has wired them.
            var holder = new GameObject("SectorLoad") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);
            holder.transform.SetParent(root, false);

            var sector = UnityEngine.Object.Instantiate(entry.prefab, holder.transform);
            ActiveSector = sector;
            // Loaded from here: a sector completing inside its own Setup must already be unloadable.
            phase = Phase.Loaded;
            // Inject the host's session-lifetime references — the sector reads them, never builds or owns them.
            sector.Initialize(Units, Objectives, Profile.presentation, entry.config, Frame, hero);

            this.onSectorComplete = onSectorComplete;
            if (onSectorComplete != null)
                sector.OnSectorComplete += onSectorComplete;

            // The sector only DECLARES its start via PlayerStart; the session does the entry reset.
            // It must precede Setup: the obstacle field lays out and anchors against the placed hero.
            if (hero)
                Units.RespawnShip(hero.Id, sector.PlayerStart, 0f);

            yield return sector.Setup();

            // Adopting into the arena root also moves the sector to the root's stable scene, keeping it out of the swappable locale scene.
            sector.transform.SetParent(root, true);
            UnityEngine.Object.Destroy(holder);
        }

        /// <summary>Unload the sector (run its teardown phase, destroy its content); the registries persist — pair with <see cref="LoadSector"/> for an episode reset.</summary>
        public IEnumerator UnloadSector()
        {
            Require(Phase.Loaded, nameof(UnloadSector));
            // Drop any queued player/NPC revives so a pending respawn cannot fire into the torn-down sector.
            Units.CancelPendingRespawns();
            // Old-sector transients must not survive into the next sector (they live under the session root, not the sector).
            Projectiles.ReturnAllToPool();

            yield return DestroyActiveSector(runTeardown: true);
            phase = Phase.Composed;
        }

        /// <summary>Session exit: drop the sector (without running its teardown phase) and wipe every registry.</summary>
        public IEnumerator Teardown()
        {
            if (phase is not (Phase.Composed or Phase.Loaded))
                throw new InvalidOperationException($"{nameof(Teardown)} requires a composed session; it is {phase}.");

            yield return DestroyActiveSector(runTeardown: false);

            if (Profile.presentation)
                yield return locale.RestoreBootEnvironmentAsync();

            Projectiles.ReturnAllToPool();
            Units.Clear();
            Objectives.ClearAll();
            Projectiles = null;
            Units = null;
            Objectives = null;
            phase = Phase.TornDown;
        }

        private void Require(Phase expected, string operation)
        {
            if (phase != expected)
                throw new InvalidOperationException($"{operation} requires the session to be {expected}; it is {phase}.");
        }

        private IEnumerator DestroyActiveSector(bool runTeardown)
        {
            var sector = ActiveSector;
            if (!sector) yield break;

            if (onSectorComplete != null)
                sector.OnSectorComplete -= onSectorComplete;
            onSectorComplete = null;

            if (runTeardown)
                yield return sector.Teardown();

            UnityEngine.Object.Destroy(sector.gameObject);
            ActiveSector = null;
        }
    }

    /// <summary>The in-plane frame a session's authored content is placed in: zero for the single-arena game, a per-arena offset for anything fanning sessions out across one plane.</summary>
    public readonly struct SessionFrame
    {
        public Vector2 Offset { get; }

        public SessionFrame(Vector2 offset) => Offset = offset;

        /// <summary>World position of an AUTHORED plane-space point. Live entity positions already carry the offset and round-trip through <see cref="GamePlane"/> instead.</summary>
        public Vector3 Place(Vector2 planePoint) => GamePlane.PlanePointToWorld(planePoint + Offset);
    }
}
