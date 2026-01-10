using UnityEngine;
using Utils;

namespace World
{
    public class WorldRoot : MonoBehaviour
    {
        public SphereCollider AsteroidCullingBoundary { get; private set; }
        public WorldFollow Follower { get; private set; }
        private void Awake()
        {
            AsteroidCullingBoundary = GameObject.FindGameObjectWithTag(TagNames.AsteroidCullingBoundary).GetComponent<SphereCollider>();
            Follower =  GetComponent<WorldFollow>();
        }
    }
}