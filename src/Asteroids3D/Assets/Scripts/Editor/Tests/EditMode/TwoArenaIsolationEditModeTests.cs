using System.Collections.Generic;
using AI.Scanning;
using Game;
using Game.Sessions;
using NUnit.Framework;
using Tests.Common;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Arena isolation: a consumer wired to one arena's obstacle field reads only that field, and a session frame places authored points in its own offset frame.</summary>
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
        public void ObstacleScanner_WiredToFieldA_ReadsFieldA_NotFieldB()
        {
            var fieldA = new SwappableField();
            var fieldB = new SwappableField();

            const float radiusA = 11f;
            const float radiusB = 22f;
            fieldA.Inner = new SingleObstacleStub(new DetectedObstacle(Vector3.zero, radiusA, null));
            fieldB.Inner = new SingleObstacleStub(new DetectedObstacle(Vector3.zero, radiusB, null));

            var origin = Track(new GameObject("Ship")).transform;
            var scanner = new ObstacleScanner(origin, maxSpeed: 10f, maxAccel: 5f, lookaheadTime: 2f,
                field: fieldA);

            scanner.Scan();
            Assert.AreEqual(1, scanner.DetectedCount, "scanner sees field A's single obstacle");
            Assert.AreEqual(radiusA, scanner.DetectedBuffer[0].radius, 1e-4f,
                "a consumer wired to field A reads A, never B");

            fieldB.Inner = null;
            scanner.Scan();
            Assert.AreEqual(1, scanner.DetectedCount, "field A is unaffected by clearing field B");
            Assert.AreEqual(radiusA, scanner.DetectedBuffer[0].radius, 1e-4f);

            fieldA.Inner = null;
            scanner.Scan();
            Assert.AreEqual(0, scanner.DetectedCount, "a null obstacle field senses zero obstacles");
        }

        [Test]
        public void Place_AppliesFrameOffset_ToAuthoredPlanePoints()
        {
            var offset = new Vector2(1000f, -250f);
            var frame = new SessionFrame(offset);

            var authored = new Vector2(7f, 3f);
            Assert.AreEqual(GamePlane.PlanePointToWorld(authored + offset), frame.Place(authored),
                "Place must convert an authored plane point into the frame's offset.");
        }
    }
}
