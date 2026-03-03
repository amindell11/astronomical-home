using UnityEngine;

namespace Objectives
{
    [CreateAssetMenu(fileName = "ObjectiveParams", menuName = "Game/Objective Params")]
    public class ObjectiveParams : ScriptableObject
    {
        [SerializeField] private float exploreThreshold = 0.8f;
        [SerializeField] private float extractionRadius = 10f;

        public float ExploreThreshold => exploreThreshold;
        public float ExtractionRadius => extractionRadius;
    }
}
