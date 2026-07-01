using System.Collections;
using AI;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using AICommander = AI.AICommander;

namespace Tests.PlayMode
{

[Category("MPC")]
[Category("Slow")]
public class MpcPerformancePlayModeTests : PlayModeWorldFixture
{
    private const int ShipCount = 8;
    private const int WarmupFrames = 30;
    private const int SampleFrames = 120;
    private const float SpawnRadius = 18f;

    private readonly Ship[] ships = new Ship[ShipCount];
    private readonly Navigator[] navigators = new Navigator[ShipCount];
    private readonly Vector3[] startPositions = new Vector3[ShipCount];

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();

#if UNITY_EDITOR
        for (var i = 0; i < ShipCount; i++)
        {
            var angle = (Mathf.PI * 2f * i) / ShipCount;
            var spawnPos = new Vector3(Mathf.Cos(angle) * SpawnRadius, Mathf.Sin(angle) * SpawnRadius, 0f);
            var targetPos = new Vector2(-spawnPos.x, -spawnPos.y);

            var ship = ShipTestFactory.CreateDefaultShipAt(spawnPos, Quaternion.identity, useMpcPilot: true, team: i);
            Assert.That(ship, Is.Not.Null, $"Failed to create MPC test ship {i}");

            var cmdr = ship.Commander as AICommander;
            Assert.That(cmdr, Is.Not.Null, $"Ship {i} commander should be an AICommander");

            cmdr.SetRegistry(new StubShipRegistry());
            cmdr.Brain.enabled = false;

            var navigator = cmdr.Navigator as Navigator;
            Assert.That(navigator, Is.Not.Null, $"Ship {i} navigator should be a Navigator");

            navigator.SetNavigationPoint(targetPos);

            ships[i] = ship;
            navigators[i] = navigator;
            startPositions[i] = ship.transform.position;
        }
#else
        Assert.Ignore("MpcPerformancePlayModeTests requires the Unity Editor (uses AssetDatabase).");
#endif
    }

    [TearDown]
    public override void TearDown()
    {
        for (var i = 0; i < ships.Length; i++)
        {
            ShipTestFactory.DestroyShip(ships[i]);
        }

        base.TearDown();
    }

    [UnityTest]
    public IEnumerator MpcEightShip_PerfProbe_LogsSolveCost()
    {
        for (var i = 0; i < WarmupFrames; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        var totalSolveMs = 0f;
        var maxShipSolveMs = 0f;
        var maxFrameSolveMs = 0f;

        for (var frame = 0; frame < SampleFrames; frame++)
        {
            yield return new WaitForFixedUpdate();

            var frameSolveMs = 0f;
            for (var i = 0; i < navigators.Length; i++)
            {
                var solveMs = navigators[i].lastSolveTimeMs;
                totalSolveMs += solveMs;
                frameSolveMs += solveMs;
                maxShipSolveMs = Mathf.Max(maxShipSolveMs, solveMs);
            }

            maxFrameSolveMs = Mathf.Max(maxFrameSolveMs, frameSolveMs);
        }

        var avgShipSolveMs = totalSolveMs / (SampleFrames * ShipCount);
        var avgFrameSolveMs = totalSolveMs / SampleFrames;

        var movedShips = 0;
        for (var i = 0; i < ships.Length; i++)
        {
            if (Vector3.Distance(startPositions[i], ships[i].transform.position) > 0.5f)
            {
                movedShips++;
            }
        }

        Debug.Log(
            $"MPC perf probe | ships={ShipCount} warmupFrames={WarmupFrames} sampleFrames={SampleFrames} " +
            $"avgShipSolveMs={avgShipSolveMs:F3} avgFrameSolveMs={avgFrameSolveMs:F3} " +
            $"maxShipSolveMs={maxShipSolveMs:F3} maxFrameSolveMs={maxFrameSolveMs:F3} movedShips={movedShips}/{ShipCount}");

        Assert.That(avgShipSolveMs, Is.GreaterThan(0f), "Perf probe should observe non-zero MPC solve time.");
        Assert.That(movedShips, Is.GreaterThanOrEqualTo(ShipCount / 2), "Perf probe should keep most ships actively navigating.");
    }
}

} // namespace Tests.PlayMode
