using System.Collections;
using Asteroids.Fields;
using Game.Encounters;
using Objectives;
using Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Sectors
{
    /// <summary>
    /// Combat sector: spawns enemies, orchestrates encounter sequence.
    /// Player, camera, and UI overlay are handled by PlaySector.
    /// Ships persist across encounter transitions; encounters own only objective GameObjects.
    /// </summary>
    public class CombatSectorManager : PlaySector
    {
        [Header("Combat Settings")]
        [SerializeField] private Ship enemyTemplate;
        [SerializeField] private Ships.Command.Commander enemyCommander;

        [Header("Environment")]
        [SerializeField] private World.WorldRoot worldPrefab;
        [SerializeField] private UpdatingAsteroidField updatingAsteroidFieldPrefab;

        [Header("Enemy Spawn (Plane Space)")]
        [SerializeField] private Vector2 enemySpawnPosition = new Vector2(0f, 50f);
        [SerializeField] private Vector2 chaserSpawnPosition = new Vector2(50f, 50f);

        [Header("Ship Spawn Settings")]
        [SerializeField] private ShipSpawnerSettings spawnerSettings;

        [Header("Encounters")]
        [SerializeField] private Encounter[] encounters;

        private Ship enemy, chaser;
        private UpdatingAsteroidField updatingAsteroidFieldInstance;
        private Encounter activeEncounter;
        private int encounterIndex;
        private UI.MinimapObjectiveMarker marker;

        protected override IEnumerator OnSetup()
        {
            if (Config.LoadScene)
                yield return Services.EnvironmentService.LoadSceneAsync(Config.SceneName);
            if (worldPrefab)
                Services.EnvironmentService.SpawnWorld(worldPrefab);

            yield return base.OnSetup();

            enemy = Services.UnitService.SpawnShip(enemyTemplate, enemyCommander, shipSettings, 1, GamePlane.PlanePointToWorld(enemySpawnPosition), GamePlane.Rotation);
            chaser = Services.UnitService.SpawnShip(enemyTemplate, enemyCommander, shipSettings, 1, GamePlane.PlanePointToWorld(chaserSpawnPosition), GamePlane.Rotation);
            chaser.gameObject.SetActive(false);

            InitializeAsteroidField();

            player.Damage.OnDeath += (_, __) =>
            {
                activeEncounter?.Fail();
                CompleteSector(SectorResult.Failed("player_died"));
            };

            if (spawnerSettings)
            {
                enemy.Damage.OnDeath += (s1, _) =>
                    Services.UnitService.WaitAndRespawnShip(s1,
                        Random.insideUnitCircle * spawnerSettings.offscreenDistance + GamePlane.WorldPointToPlane(Services.EnvironmentService.WorldFollowerTransform.position),
                        0, spawnerSettings.enemyRespawnDelay);
            }

            var respawnDelay = spawnerSettings ? spawnerSettings.enemyRespawnDelay : 3f;
            chaser.Damage.OnDeath += (s1, _) =>
                Services.UnitService.WaitAndRespawnShip(s1,
                    Random.insideUnitCircle * (spawnerSettings ? spawnerSettings.offscreenDistance : 30f)
                        + GamePlane.WorldPointToPlane(Services.EnvironmentService.WorldFollowerTransform.position),
                    0, respawnDelay);

            WireObjectiveMarker();

            yield return StartEncounter(0);
        }

        private void InitializeAsteroidField()
        {
            if (!updatingAsteroidFieldPrefab)
                return;

            var cullingBoundary = Services.EnvironmentService.World
                ? Services.EnvironmentService.World.AsteroidCullingBoundary
                : null;

            if (!cullingBoundary)
            {
                Debug.LogWarning("Updating asteroid field prefab is assigned, but no AsteroidCullingBoundary exists in the spawned world.");
                return;
            }

            updatingAsteroidFieldInstance = Instantiate(updatingAsteroidFieldPrefab);
            updatingAsteroidFieldInstance.Initialize(cullingBoundary);
            updatingAsteroidFieldInstance.SetWorldAnchor(Services.EnvironmentService.WorldFollowerTransform);
            updatingAsteroidFieldInstance.CurrentAnchorPos = () =>
                player ? GamePlane.ProjectOntoPlane(player.transform.position) : updatingAsteroidFieldInstance.transform.position;
        }

        private IEnumerator StartEncounter(int index)
        {
            encounterIndex = index;
            var encounter = Instantiate(encounters[index]);
            encounter.Initialize(Services, player);

            if (encounter is ExtractionEncounter extraction)
                extraction.SetChaser(chaser);

            encounter.OnEncounterComplete += HandleEncounterComplete;
            yield return encounter.Setup();
            activeEncounter = encounter;

            if (marker)
                SetMarkerTarget(marker, Services.ObjectiveService.CurrentState ?? ObjectiveType.Explore);
        }

        private void HandleEncounterComplete(Encounter enc, EncounterResult result)
        {
            if (result == EncounterResult.Failed)
            {
                CompleteSector(SectorResult.Failed("encounter_failed"));
                return;
            }
            StartCoroutine(TransitionToNextEncounter(enc));
        }

        private IEnumerator TransitionToNextEncounter(Encounter enc)
        {
            yield return enc.Teardown();
            Destroy(enc.gameObject);
            activeEncounter = null;

            encounterIndex++;
            if (encounterIndex < encounters.Length)
                yield return StartEncounter(encounterIndex);
            else
                CompleteSector(SectorResult.Extracted());
        }

        private void WireObjectiveMarker()
        {
            var overlay = Services.UIService.ActiveOverlay;
            if (!overlay || !overlay.ObjectiveMarker) return;

            marker = overlay.ObjectiveMarker;

            Services.ObjectiveService.OnStateChanged += (_, to) => SetMarkerTarget(marker, to);
        }

        private void SetMarkerTarget(UI.MinimapObjectiveMarker m, ObjectiveType state)
        {
            switch (state)
            {
                case ObjectiveType.Explore:
                case ObjectiveType.KeyAcquired:
                case ObjectiveType.ExtractionChallenge:
                    m.SetTarget(activeEncounter ? activeEncounter.ObjectiveTarget : null);
                    break;
                default:
                    m.SetTarget(null);
                    break;
            }
        }

        protected override IEnumerator OnTeardown()
        {
            if (activeEncounter != null)
            {
                yield return activeEncounter.Teardown();
                Destroy(activeEncounter.gameObject);
                activeEncounter = null;
            }

            Services.ObjectiveService.Clear();

            if (updatingAsteroidFieldInstance) Destroy(updatingAsteroidFieldInstance.gameObject);

            yield return base.OnTeardown();

            if (Config.LoadScene)
                yield return Services.EnvironmentService.UnloadSceneAsync(Config.SceneName);

            enemy = null;
            chaser = null;
            updatingAsteroidFieldInstance = null;
        }
    }
}
