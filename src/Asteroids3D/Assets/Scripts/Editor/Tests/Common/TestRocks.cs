#if UNITY_EDITOR
using System.Reflection;
using Asteroids;
using Game;
using UnityEngine;

namespace Tests.Common
{
    /// <summary>Builds pool-shaped test asteroids without the spawner stack: required components plus a Rigidbody, Awake forced (EditMode runs no lifecycle), spawn epoch settable to simulate pooled reuse.</summary>
    public static class TestRocks
    {
        private static readonly MethodInfo AwakeMethod =
            typeof(AsteroidController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo EpochProperty =
            typeof(AsteroidController).GetProperty(nameof(AsteroidController.SpawnEpoch));

        public static AsteroidController Spawn(Vector2 planePos)
        {
            var go = new GameObject("TestRock");
            go.AddComponent<Rigidbody>();
            var rock = go.AddComponent<AsteroidController>();
            AwakeMethod.Invoke(rock, null);
            go.transform.position = GamePlane.PlanePointToWorld(planePos);
            return rock;
        }

        /// <summary>Simulates the pool handing the component to a new rock.</summary>
        public static void BumpEpoch(AsteroidController rock) =>
            EpochProperty.SetValue(rock, rock.SpawnEpoch + 1);
    }
}
#endif
