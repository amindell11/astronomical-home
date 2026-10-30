using System.Collections;
using AI;
using AI.Scanning;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using NUnit.Framework;
using Ships;
using Substrate;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{

[Category("AI")]
public class ScannerPlayModeTests : PlayModeWorldFixture
{
    private Ship ship;
    private AICommander cmdr;

    private const float ScanTimeoutSec = 3f;
    private const string GrenadesPrefabPath = "Assets/Prefabs/Weapons/Grenades.prefab";

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();

#if UNITY_EDITOR
        ship = ShipTestFactory.CreateDefaultShip(Projectiles);
        cmdr = ship.Commander as AICommander;

        // Scout.Initialize() (and therefore obstacleScanner) is gated on sensing being wired.
        cmdr.SetSensing(new StubShipRegistry(), Field);
#else
        Assert.Ignore("ScannerPlayModeTests requires the Unity Editor (uses AssetDatabase).");
#endif
    }

    [TearDown]
    public override void TearDown()
    {
        ShipTestFactory.DestroyShip(ship);
        base.TearDown();
    }

    [UnityTest]
    [Category("Smoke")]
    public IEnumerator ObstacleScanner_DetectsNearbyObstacle_WithinTimeout()
    {
        // The obstacle scanner queries the obstacle field it was wired with, not physics colliders.
        // Inject a stub that reports one obstacle at the ship's location so the merge path fills.
        ObstacleField.Inner = new StubObstacleField(new DetectedObstacle(ship.transform.position, 5f, null));

        yield return AsyncAssert.WaitUntil(
            () => cmdr.Scout.ObstacleScan.count > 0,
            ScanTimeoutSec,
            $"Scanner should detect the stub field obstacle within {ScanTimeoutSec}s",
            useFixedUpdate: true);
    }

    [UnityTest]
    public IEnumerator ObstacleScan_LeadsWithOwnChargeThenBlastKeepOut_AndDropsEachWhenItEnds()
    {
        var rock = new DetectedObstacle(ship.transform.position + GamePlane.PlaneDirToWorld(Vector2.up * 8f), 3f, null);
        ObstacleField.Inner = new StubObstacleField(rock);
        ship.Reequip(TestAssets.Load<Grenades>(GrenadesPrefabPath), null);
        var launcher = (Grenades)ship.Weapons.Primary;

        Grenade charge = null;
        launcher.Launched += flight => charge = (Grenade)flight;
        var aim = ship.transform.position + GamePlane.PlaneDirToWorld(Vector2.right * 20f);
        launcher.HandleTrigger(new WeaponCommand { held = true, pressed = true, targetPoint = aim }, Projectiles);
        Assert.IsNotNull(charge, "the launcher launched a charge");
        ConcussionWave blast = null;
        charge.Spawned += (child, _) => blast = (ConcussionWave)child;

        var sawCharge = false;
        var sawBlast = false;
        var dropped = false;
        // Frame-stepped, not fixed-stepped: the scout rebuilds its list in Update.
        var deadline = Time.time + ScanTimeoutSec;
        while (!dropped && Time.time < deadline)
        {
            yield return null;
            var scan = cmdr.Scout.ObstacleScan;
            if (Live(charge))
            {
                AssertLeadsRock(scan, rock, charge.TargetPoint, launcher.MinLobDistance, "charge");
                sawCharge = true;
            }
            else if (Live(blast))
            {
                AssertLeadsRock(scan, rock, blast.transform.position, launcher.BlastRadius, "blast");
                sawBlast = true;
            }
            else if (blast)
            {
                Assert.AreEqual(1, scan.count, "with charge and blast gone only the rock is left");
                dropped = true;
            }
        }

        Assert.IsTrue(sawCharge, "the charge's circle was seen in flight");
        Assert.IsTrue(sawBlast, "the blast's circle was seen");
        Assert.IsTrue(dropped, $"the blast ended within {ScanTimeoutSec}s");
    }

    private static bool Live(Component transient) => transient && transient.gameObject.activeSelf;

    private static void AssertLeadsRock(ObstacleScan scan, DetectedObstacle rock, Vector3 center, float radius, string source)
    {
        Assert.AreEqual(2, scan.count, $"the {source}'s circle and the rock");
        var circle = scan.buffer[0];
        Assert.Less(Vector2.Distance(GamePlane.WorldPointToPlane(center), circle.position), 1e-3f, $"the {source}'s circle is centred on it");
        Assert.AreEqual(radius, circle.radius, 1e-4f, $"the {source}'s circle radius");
        Assert.AreEqual(rock.position, scan.buffer[1].position, "the rock comes after the circle");
    }

    // Reports a single fixed obstacle whenever it falls inside the query box.
    private sealed class StubObstacleField : IObstacleField
    {
        private readonly DetectedObstacle obstacle;
        public StubObstacleField(DetectedObstacle obstacle) => this.obstacle = obstacle;

        public int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return 0;
            if (Mathf.Abs(obstacle.position.x - centerPlane.x) > halfExtent
                || Mathf.Abs(obstacle.position.y - centerPlane.y) > halfExtent) return 0;
            buffer[0] = obstacle;
            return 1;
        }
    }
}

}
