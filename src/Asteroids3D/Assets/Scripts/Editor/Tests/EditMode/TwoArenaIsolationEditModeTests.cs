using System.Collections.Generic;
using AI.Scanning;
using Game;
using NUnit.Framework;
using Tests.Common;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>The net-new guarantee of the WorldHandle cut: a consumer wired to one world reads only that world's obstacle field.</summary>
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
        public void ObstacleScanner_WiredToWorldA_ReadsWorldAField_NotWorldB()
        {
            var worldA = TestWorld.On(field: new TestWorld.SwappableField());
            var worldB = TestWorld.On(field: new TestWorld.SwappableField());
            var fieldA = (TestWorld.SwappableField)worldA.ObstacleField;
            var fieldB = (TestWorld.SwappableField)worldB.ObstacleField;

            const float radiusA = 11f;
            const float radiusB = 22f;
            fieldA.Inner = new SingleObstacleStub(new DetectedObstacle(Vector3.zero, radiusA, null));
            fieldB.Inner = new SingleObstacleStub(new DetectedObstacle(Vector3.zero, radiusB, null));

            var origin = Track(new GameObject("Ship")).transform;
            var scanner = new ObstacleScanner(origin, maxSpeed: 10f, maxAccel: 5f, lookaheadTime: 2f,
                field: worldA.ObstacleField);

            scanner.Scan();
            Assert.AreEqual(1, scanner.DetectedCount, "scanner sees world A's single obstacle");
            Assert.AreEqual(radiusA, scanner.DetectedBuffer[0].radius, 1e-4f,
                "a consumer wired to world A reads A's field, never B's");

            fieldB.Inner = null;
            scanner.Scan();
            Assert.AreEqual(1, scanner.DetectedCount, "world A is unaffected by clearing world B");
            Assert.AreEqual(radiusA, scanner.DetectedBuffer[0].radius, 1e-4f);

            fieldA.Inner = null;
            scanner.Scan();
            Assert.AreEqual(0, scanner.DetectedCount, "a null world field senses zero obstacles");
        }

        [Test]
        public void Place_AppliesWorldOffset_ToAuthoredPlanePoints()
        {
            var offset = new Vector2(1000f, -250f);
            var world = new Game.Services.WorldHandle(offset, new StubShipRegistry(), null);

            var authored = new Vector2(7f, 3f);
            Assert.AreEqual(GamePlane.PlanePointToWorld(authored + offset), world.Place(authored),
                "Place must convert an authored plane point into the world's offset frame.");
        }
    }
}
