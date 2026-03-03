using UnityEngine;

namespace Objectives
{
    [CreateAssetMenu(fileName = "ObjectiveParams", menuName = "Game/Objective Params")]
    public class ObjectiveParams : ScriptableObject
    {
        [Header("Key")]
        [SerializeField] private float keySpawnRadius = 30f;
        [SerializeField] private float keyPickupDistance = 4f;

        [Header("Extraction")]
        [SerializeField] private float extractionRadius = 10f;
        [SerializeField] private float extractionBlockDistance = 20f;

        public float KeySpawnRadius => keySpawnRadius;
        public float KeyPickupDistance => keyPickupDistance;
        public float ExtractionRadius => extractionRadius;
        public float ExtractionBlockDistance => extractionBlockDistance;

        private void OnValidate()
        {
            keySpawnRadius = Mathf.Max(0f, keySpawnRadius);
            keyPickupDistance = Mathf.Max(0f, keyPickupDistance);
            extractionRadius = Mathf.Max(0f, extractionRadius);
            extractionBlockDistance = Mathf.Max(0f, extractionBlockDistance);
        }
    }
}
