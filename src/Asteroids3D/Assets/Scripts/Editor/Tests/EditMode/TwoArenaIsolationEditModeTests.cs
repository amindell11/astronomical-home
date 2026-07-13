using System.Collections.Generic;
using AI.Scanning;
using Game;
using NUnit.Framework;
using Tests.Common;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The net-new behavioral guarantee of the ArenaContext hard-cut: two arenas each carry their own
    /// obstacle-field provider, and a consumer wired to one reads only that arena's field — the frame
    /// the process-wide <c>ObstacleFields.Active</c> static could never provide.
    /// </summary>
    [Category("Core")]
    public class TwoArenaIsolationEditModeTests
    {
        private sealed class SingleObstacleStub : IObstacleField
        {
            private readonly DetectedObstacle obstacle;
            public SingleObstacleStub(DetectedObstacle obstacle) => this.obstacle = obstacle;

            public int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer)
            {
                if (buffer == null || buffer.Length == 0) return 0;
                buffer[0] = obstacle;
                return 1;
            }
        }

        private readonly List<GameObject> created = new();
        private GameObject Track(GameObject go) { created.Add(go); return go; }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go) Object.DestroyImmediate(go);
            created.Clear();
        }

        [Test]
        public void ObstacleScanner_WiredToArenaA_ReadsArenaAField_NotArenaB()
        {
            var arenaA = TestArena.On(Track(new GameObject("ArenaA")));
            var arenaB = TestArena.On(Track(new GameObject("ArenaB")));

            const float radiusA = 11f;
            const float radiusB = 22f;
            arenaA.ObstacleField = new SingleObstacleStub(new DetectedObstacle(Vector3.zero, radiusA, null));
            arenaB.ObstacleField = new SingleObstacleStub(new DetectedObstacle(Vector3.zero, radiusB, null));

            var origin = Track(new GameObject("Ship")).transform;
            var scanner = new ObstacleScanner(origin, maxSpeed: 10f, maxAccel: 5f, lookaheadTime: 2f, arena: arenaA);

            scanner.Scan();
            Assert.AreEqual(1, scanner.DetectedCount, "scanner sees arena A's single obstacle");
            Assert.AreEqual(radiusA, scanner.DetectedBuffer[0].radius, 1e-4f,
                "a consumer wired to arena A reads A's field, never B's");

            arenaB.ObstacleField = null;
            scanner.Scan();
            Assert.AreEqual(1, scanner.DetectedCount, "arena A is unaffected by clearing arena B");
            Assert.AreEqual(radiusA, scanner.DetectedBuffer[0].radius, 1e-4f);

            arenaA.ObstacleField = null;
            scanner.Scan();
            Assert.AreEqual(0, scanner.DetectedCount, "a null arena field senses zero obstacles");
        }

        [Test]
        public void Place_AppliesArenaOffset_ToAuthoredPlanePoints()
        {
            var host = Track(new GameObject("Arena"));
            var offset = new Vector2(1000f, -250f);
            var arena = new Game.Services.ArenaContext(offset, new StubShipRegistry(),
                host.AddComponent<Movement.MPC.Field.NavFieldService>(), host.transform);

            var authored = new Vector2(7f, 3f);
            Assert.AreEqual(GamePlane.PlanePointToWorld(authored + offset), arena.Place(authored),
                "Place must convert an authored plane point into the arena's offset world frame.");
        }
    }
}
