using System.Collections;
using UnityEngine;
using World;

namespace Game.Services
{
    public interface IEnvironmentService
    {
        /// <summary>Currently active world root, if any.</summary>
        WorldRoot World { get; }

        /// <summary>World follower transform for camera/asteroid anchoring.</summary>
        Transform WorldFollowerTransform { get; }

        /// <summary>Load a scene additively. Yields until complete.</summary>
        IEnumerator LoadSceneAsync(string sceneName);

        /// <summary>Unload a previously session-loaded scene. Yields until complete.</summary>
        IEnumerator UnloadSceneAsync(string sceneName);

        /// <summary>Instantiate a WorldRoot prefab.</summary>
        void SpawnWorld(WorldRoot prefab);

        /// <summary>Take ownership of an already-instantiated WorldRoot (authored as a sector child).</summary>
        void AdoptWorld(WorldRoot existing);

        /// <summary>Destroy world and clear tracked state. Does NOT unload scenes.</summary>
        void Clear();
    }
}
