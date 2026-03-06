using System.Collections;
using Asteroids.Fields;
using Diagnostics.Performance;
using Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Sectors
{
    /// <summary>
    /// Arena sector: spawns two AI teams that fight each other continuously.
    /// When playerParticipates is false, the player ship uses an AI commander
    /// and acts as the camera subject for spectating.
    /// </summary>
    public class ArenaSectorManager : PlaySector
    {
        [Header("Arena Settings")]
        [SerializeField] private Ship enemyTemplate;
        [SerializeField] private Ships.Command.Commander aiCommander;
        [SerializeField] private int teamACount = 4;
        [SerializeField] private int teamBCount = 4;
        [SerializeField] private float spawnRadius = 60f;
        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private bool playerParticipates;

        [Header("Debug")]
        [SerializeField] private bool enableDebugOverlay = true;

        [Header("Environment")]
        [SerializeField] private World.WorldRoot worldPrefab;
        [SerializeField] private UpdatingAsteroidField updatingAsteroidFieldPrefab;

        private readonly System.Collections.Generic.List<Ship> arenaShips = new();
        private UpdatingAsteroidField asteroidFieldInstance;
        private AI.Debug.ArenaDebugOverlay debugOverlay;
        private Cameras.ArenaSpectatorInput spectatorInput;
        private LatencyProfilingSettings profilingSettings;

        public void ApplyLatencyProfilingSettings(LatencyProfilingSettings settings)
        {
            profilingSettings = settings;
            if (settings == null)
                return;

            teamACount = settings.TeamACount;
            teamBCount = settings.TeamBCount;
            spawnRadius = settings.SpawnRadius;
            respawnDelay = settings.RespawnDelay;
            playerParticipates = false;
            enableDebugOverlay = !settings.DisableDebugOverlay;
        }

        protected override IEnumerator OnSetup()
        {
            if (profilingSettings != null)
                Random.InitState(profilingSettings.Seed);

            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);

            // If player is spectating, use AI commander instead of player input
            if (!playerParticipates)
                playerCommander = aiCommander;

            yield return base.OnSetup();

            // Player is team 0; spawn additional team A ships (team 0)
            var teamAExtra = teamACount - 1;
            SpawnTeam(0, teamAExtra, -1f);

            // Spawn team B (team 1)
            SpawnTeam(1, teamBCount, 1f);

            InitializeAsteroidField();

            // Wire respawn for all ships
            WireRespawn(player);
            foreach (var ship in arenaShips)
                WireRespawn(ship);

            // Debug overlay
            if (enableDebugOverlay)
            {
                debugOverlay = gameObject.AddComponent<AI.Debug.ArenaDebugOverlay>();
                if (player) debugOverlay.RegisterShip(player);
                foreach (var ship in arenaShips)
                    debugOverlay.RegisterShip(ship);
            }

            // Spectator input — attach to the ObserverCam so it can control it
            var observer = Services.CameraService.GetCamera<Cameras.ObserverCam>(Cameras.CameraTag.Observer);
            if (observer)
            {
                // Remove default input handler; arena uses its own
                var defaultInput = observer.GetComponent<Cameras.ObserverCamInputHandler>();
                if (defaultInput) Destroy(defaultInput);

                spectatorInput = observer.gameObject.AddComponent<Cameras.ArenaSpectatorInput>();
                var allShips = new System.Collections.Generic.List<Ship>();
                if (player) allShips.Add(player);
                allShips.AddRange(arenaShips);
                spectatorInput.SetShips(allShips);

                // Start locked to the player ship
                observer.SetLockCameraToSubject(true);
                observer.SetLockZoomToSubject(true);
            }
        }

        private void SpawnTeam(int team, int count, float sideSign)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * (i + 0.5f) / count;
                var offset = new Vector2(
                    Mathf.Cos(angle) * spawnRadius * sideSign,
                    Mathf.Sin(angle) * spawnRadius);

                var ship = Services.UnitService.SpawnShip(
                    enemyTemplate, aiCommander, shipSettings, team,
                    GamePlane.PlanePointToWorld(offset),
                    Quaternion.identity);

                arenaShips.Add(ship);
            }
        }

        private void WireRespawn(Ship ship)
        {
            if (!ship) return;
            ship.Damage.OnDeath += (deadShip, _) =>
                Services.UnitService.WaitAndRespawnShip(deadShip,
                    Random.insideUnitCircle * spawnRadius * 0.5f,
                    0, respawnDelay);
        }

        private void InitializeAsteroidField()
        {
            if (!updatingAsteroidFieldPrefab) return;

            var cullingBoundary = Services.EnvironmentService.World
                ? Services.EnvironmentService.World.AsteroidCullingBoundary
                : null;

            if (!cullingBoundary) return;

            asteroidFieldInstance = Instantiate(updatingAsteroidFieldPrefab);
            asteroidFieldInstance.ApplyLatencyProfilingSettings(profilingSettings);
            asteroidFieldInstance.Initialize(cullingBoundary);
            asteroidFieldInstance.SetWorldAnchor(Services.EnvironmentService.WorldFollowerTransform);
            asteroidFieldInstance.CurrentAnchorPos = () =>
                player ? GamePlane.ProjectOntoPlane(player.transform.position) : asteroidFieldInstance.transform.position;
        }

        protected override IEnumerator OnTeardown()
        {
            if (spectatorInput)
                Destroy(spectatorInput);

            if (debugOverlay)
                Destroy(debugOverlay);

            if (asteroidFieldInstance)
                Destroy(asteroidFieldInstance.gameObject);

            yield return base.OnTeardown();

            if (Config.LoadScene)
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);

            arenaShips.Clear();
            asteroidFieldInstance = null;
            debugOverlay = null;
            spectatorInput = null;
        }
    }
}
