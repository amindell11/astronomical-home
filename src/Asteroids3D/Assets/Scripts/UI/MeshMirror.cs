using Asteroids;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Place on a child GameObject (e.g. minimap mesh) to keep its mesh
    /// in sync with the parent AsteroidController's mesh after each spawn.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class MeshMirror : MonoBehaviour
    {
        private AsteroidController source;
        private MeshFilter meshFilter;
        private MeshCollider sourceCollider;

        private void Awake()
        {
            source = GetComponentInParent<AsteroidController>();
            meshFilter = GetComponent<MeshFilter>();
            if (source) sourceCollider = source.GetComponent<MeshCollider>();
        }

        private void OnEnable()
        {
            if (source) source.OnInitialized += SyncMesh;
        }

        private void OnDisable()
        {
            if (source) source.OnInitialized -= SyncMesh;
        }

        private void SyncMesh()
        {
            meshFilter.sharedMesh = sourceCollider && sourceCollider.sharedMesh
                ? sourceCollider.sharedMesh
                : source.CurrentMesh;
        }
    }
}
