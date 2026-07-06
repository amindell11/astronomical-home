using System.Collections;
using UnityEngine;
using World;

namespace Game.Services
{
    public interface IObstacleField
    {
    }

    public interface IObstacleFieldProvider
    {
        IObstacleField ObstacleField { get; }
    }

    public interface IEnvironmentService : IObstacleFieldProvider
    {
        /// <summary>Currently active world root, if any.</summary>
        WorldRoot World { get; }

        /// <summary>World follower transform for camera/respawn anchoring.</summary>
        Transform WorldFollowerTransform { get; }

        /// <summary>Load a scene additively. Yields until complete.</summary>
        IEnumerator LoadSceneAsync(string sceneName);

        /// <summary>Unload a previously session-loaded scene. Yields until complete.</summary>
        IEnumerator UnloadSceneAsync(string sceneName);

        /// <summary>Instantiate a WorldRoot prefab.</summary>
        void SpawnWorld(WorldRoot prefab);

        /// <summary>Take ownership of an already-instantiated WorldRoot (authored as a sector child).</summary>
        void AdoptWorld(WorldRoot existing);

        /// <summary>Register the currently active obstacle field, if any.</summary>
        void RegisterObstacleField(IObstacleField obstacleField);

        /// <summary>Destroy world and clear tracked state. Does NOT unload scenes.</summary>
        void Clear();
    }
}
