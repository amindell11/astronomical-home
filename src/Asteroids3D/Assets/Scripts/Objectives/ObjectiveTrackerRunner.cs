using System;
using Ships;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Objectives
{
    /// <summary>
    /// Scene-level owner for the objective tracker.
    /// Implements narrow objective interfaces directly (no context god object).
    ///
    /// MVP behavior:
    /// - Random key spawn in Explore.
    /// - Key pickup by distance.
    /// - Extraction challenge spawns one chaser.
    /// - Extraction auto-completes in fixed zone unless chaser is too close.
    /// - Extracted/Failed both restart the same encounter.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class ObjectiveTrackerRunner : MonoBehaviour,
        IObjectiveTrackerAdapter,
        IKeyTracker,
        IPlayerPosition,
        IExtractionZone,
        IPlayerAlive,
        IExtractionBlocker,
        IExtractionChaserSpawner
    {
        [Header("Refs")]
        [SerializeField] private Transform player;
        [SerializeField] private Ship playerShip;
        [SerializeField] private Transform extractionZone;
        [SerializeField] private Transform chaser;
        [SerializeField] private GameObject keyVisual;
        [SerializeField] private ObjectiveParams parameters;

        [Header("Spawn")]
        [SerializeField] private Vector3 keySpawnCenter = Vector3.zero;
        [SerializeField] private Transform keySpawnCenterTransform;

        private ObjectiveTracker tracker;
        private Vector3 keyPosition;
        private bool hasKey;
        private bool chaserSpawned;

        public event Action<ObjectiveType, ObjectiveType> OnStateChanged;

        public ObjectiveType CurrentState => tracker?.CurrentState ?? ObjectiveType.Explore;
        public bool PlayerHasKey => hasKey;
        public Vector3 Position => player ? player.position : Vector3.zero;
        Vector3 IExtractionZone.Position => extractionZone ? extractionZone.position : Vector3.zero;
        public bool IsAlive => playerShip == null || playerShip.gameObject.activeSelf;

        public bool IsExtractionBlocked
        {
            get
            {
                if (!chaserSpawned || !chaser || !player || parameters == null)
                    return false;

                return Vector3.Distance(chaser.position, player.position) <= parameters.ExtractionBlockDistance;
            }
        }

        private void Awake()
        {
            if (playerShip == null && player != null)
                playerShip = player.GetComponent<Ship>();

            if (parameters == null)
            {
                Debug.LogError("[ObjectiveTrackerRunner] Missing ObjectiveParams reference.", this);
                enabled = false;
                return;
            }

            SpawnKeyAtRandomPosition();
            EnsureKeyVisualPosition();
            EnsureChaserState(active: false);

            var factory = new ObjectiveStateFactory(this, this, this, this, this, parameters);
            tracker = new ObjectiveTracker(MissionDefinition.CreateDefault(), factory, this);
            tracker.OnStateChanged += HandleStateChanged;
        }

        private void Update()
        {
            if (tracker == null)
                return;

            UpdateKeyPickup();
            tracker.Tick(Time.deltaTime);

            if (tracker.CurrentState == ObjectiveType.Extracted || tracker.CurrentState == ObjectiveType.Failed)
                RestartEncounter();
        }

        public void SpawnChaser()
        {
            chaserSpawned = true;
            EnsureChaserState(active: true);
        }

        private void RestartEncounter()
        {
            hasKey = false;
            chaserSpawned = false;
            EnsureChaserState(active: false);
            SpawnKeyAtRandomPosition();
            EnsureKeyVisualPosition();

            if (playerShip != null && !playerShip.gameObject.activeSelf)
                playerShip.ResetShip();

            tracker.Restart();
        }

        private void UpdateKeyPickup()
        {
            if (hasKey || player == null || parameters == null)
                return;

            if (Vector3.Distance(player.position, keyPosition) <= parameters.KeyPickupDistance)
            {
                hasKey = true;
                if (keyVisual != null)
                    keyVisual.SetActive(false);
            }
        }

        private void SpawnKeyAtRandomPosition()
        {
            var center = keySpawnCenterTransform ? keySpawnCenterTransform.position : keySpawnCenter;
            var offset2D = Random.insideUnitCircle * parameters.KeySpawnRadius;
            keyPosition = new Vector3(center.x + offset2D.x, center.y, center.z + offset2D.y);

            if (keyVisual != null)
                keyVisual.SetActive(true);
        }

        private void EnsureKeyVisualPosition()
        {
            if (keyVisual != null)
                keyVisual.transform.position = keyPosition;
        }

        private void EnsureChaserState(bool active)
        {
            if (chaser != null)
                chaser.gameObject.SetActive(active);
        }

        private void HandleStateChanged(ObjectiveType from, ObjectiveType to)
        {
            OnStateChanged?.Invoke(from, to);
        }

        private void OnDestroy()
        {
            if (tracker != null)
                tracker.OnStateChanged -= HandleStateChanged;
        }
    }
}
