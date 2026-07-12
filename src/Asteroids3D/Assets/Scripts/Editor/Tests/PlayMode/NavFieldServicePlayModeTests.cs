using System.Collections;
using System.Collections.Generic;
using AI.Scanning;
using Game;
using Movement.MPC.Field;
using NUnit.Framework;
using Tests.PlayMode.Common;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Track B3 — NavFieldService: bakes a cost-to-go field off B2's live obstacle query,
    /// double-buffered off the main thread, and reflects obstacle removal on rebuild.
    /// </summary>
    [Category("AI")]
    public class NavFieldServicePlayModeTests : PlayModeWorldFixture
    {
        private NavFieldService service;
        private GameObject targetGo;

        // Live obstacle source stub: returns whatever plane obstacles are currently in the list
        // that fall inside the query AABB (mirrors UpdatingAsteroidField.QueryObstacles' contract).
        private sealed class StubField : IObstacleField
        {
            public readonly List<(Vector2 pos, float radius)> obstacles = new();

            public int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer)
            {
                var n = 0;
                foreach (var o in obstacles)
                {
                    if (n >= buffer.Length) break;
                    if (Mathf.Abs(o.pos.x - centerPlane.x) > halfExtent ||
                        Mathf.Abs(o.pos.y - centerPlane.y) > halfExtent) continue;
                    buffer[n++] = new DetectedObstacle(GamePlane.PlanePointToWorld(o.pos), o.radius, null);
                }
                return n;
            }
        }

        private readonly StubField stub = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            service = new GameObject("[NavFieldService]").AddComponent<NavFieldService>();
            targetGo = new GameObject("ChaseTarget");
            targetGo.transform.position = GamePlane.PlanePointToWorld(Vector2.zero);
            stub.obstacles.Clear();
        }

        [TearDown]
        public override void TearDown()
        {
            if (service) Object.DestroyImmediate(service.gameObject);
            if (targetGo) Object.DestroyImmediate(targetGo);
            base.TearDown();
        }

        // Cell index for a plane position within a solved field view.
        private static int Cell(in TerminalFieldData d, float2 p)
        {
            var gx = (int)math.floor((p.x - d.origin.x) / d.cellSize);
            var gy = (int)math.floor((p.y - d.origin.y) / d.cellSize);
            return gy * d.gridSize + gx;
        }

        // Pumps frames (letting Update swap completed bakes) until the target's field is solved
        // and the predicate holds, or the timeout elapses. Returns the last sampled data.
        private IEnumerator PumpUntil(System.Func<TerminalFieldData, bool> predicate,
            float timeoutSec, System.Action<TerminalFieldData> onDone)
        {
            var deadline = Time.time + timeoutSec;
            var data = default(TerminalFieldData);
            while (Time.time < deadline)
            {
                service.TryGetData(targetGo.transform, float2.zero, 3f, stub, out data);
                if (data.isValid != 0 && predicate(data)) break;
                yield return null;
            }
            onDone(data);
        }

        [UnityTest]
        public IEnumerator Bakes_ValidField_WithTargetCellCheapest()
        {
            var solved = default(TerminalFieldData);
            yield return PumpUntil(d => true, 5f, d => solved = d);

            Assert.AreEqual(1, solved.isValid, "field solved within timeout");
            var goalSample = TerminalFieldData.Sample(float2.zero, solved);
            var farSample = TerminalFieldData.Sample(new float2(60f, 0f), solved);
            Assert.Less(goalSample, farSample, "cost-to-go rises with distance from the target");
        }

        [UnityTest]
        public IEnumerator ObstacleRemoval_ReflectedOnRebuild()
        {
            // A small cluster near plane (30,0) — enough obstacles that its removal trips the
            // registry-delta rebuild trigger.
            var wallPlane = new float2(30f, 0f);
            stub.obstacles.Add((new Vector2(30f, 0f), 1.5f));
            stub.obstacles.Add((new Vector2(30f, 3f), 1.5f));
            stub.obstacles.Add((new Vector2(30f, -3f), 1.5f));

            var blockedData = default(TerminalFieldData);
            yield return PumpUntil(d => d.blocked[Cell(d, wallPlane)] != 0, 5f, d => blockedData = d);
            Assert.AreEqual(1, blockedData.blocked[Cell(blockedData, wallPlane)],
                "cluster cell is stamped blocked while the obstacles are live");

            // Destroy the cluster; the next rebuild must drop it (live reflection).
            stub.obstacles.Clear();

            var clearedData = default(TerminalFieldData);
            yield return PumpUntil(d => d.blocked[Cell(d, wallPlane)] == 0, 5f, d => clearedData = d);
            Assert.AreEqual(0, clearedData.blocked[Cell(clearedData, wallPlane)],
                "removed obstacles are no longer stamped — the field rebuilt off live query state");
        }
    }
}
